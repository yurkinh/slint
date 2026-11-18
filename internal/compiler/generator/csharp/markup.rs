// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

//! The builders of `Slint.Markup`, which write `.slint` source from C#:
//! one class for each built-in element and each component that the input exports.

use super::{Config, ident, string_literal, write_enum};
use crate::element_type::{ElementDocEntry, ElementType, PropertyLookupMode};
use crate::langtype::{Enumeration, StructName, Type};
use crate::native_class::PropertyVisibility;
use crate::object_tree::{Component, Document};
use crate::typeregister::reserved_properties;
use itertools::Itertools;
use smol_str::SmolStr;
use std::collections::{BTreeMap, BTreeSet};
use std::fmt::Write;
use std::rc::Rc;
use std::sync::Arc;

/// The public types of the `Slint` namespace that a builder would hide in code that uses both namespaces.
const SLINT_TYPES: &[&str] = &[
    "Brush",
    "Color",
    "DataTransfer",
    "Image",
    "Keys",
    "PointerEventButton",
    "StyledText",
    "Timer",
    "Window",
];

/// The types of the namespaces that a .NET project imports implicitly, such as `System.IO.Path`.
const IMPLICIT_USING_TYPES: &[&str] = &["File", "Path", "Timer"];

/// The types of `Slint.Markup` itself.
const MARKUP_TYPES: &[&str] =
    &["Element", "Expr", "ForEach", "If", "Page", "Row", "RowAction", "SlintType"];

/// The members of `Element` and `object`, which a builder method mustn't hide.
const ELEMENT_MEMBERS: &[&str] = &[
    "Add",
    "Equals",
    "GetEnumerator",
    "GetHashCode",
    "GetType",
    "Id",
    "MemberwiseClone",
    "Property",
    "SetBinding",
    "SetExpression",
    "SetHandler",
    "SetValue",
    "ToString",
];

/// How a property type appears in C#: its type, and the `SlintType` that writes and converts it.
struct Mapped {
    csharp: String,
    slint: String,
}

/// The enums that the builders use: in the library all of them, in an app only its own,
/// since the built-in ones are the library's.
struct Enums {
    builtins: bool,
    generate: BTreeMap<SmolStr, Arc<Enumeration>>,
    /// The C# expression of the file that a page imports the enums of the input from.
    import: String,
}

impl Enums {
    fn csharp(&mut self, e: &Arc<Enumeration>) -> String {
        let is_builtin = e.node.is_none();
        if is_builtin && !self.builtins {
            return format!("global::Slint.Markup.{}", enum_name(&e.name));
        }
        self.generate.insert(e.name.clone(), e.clone());
        enum_name(&e.name)
    }
}

/// The C# side of `ty`, or `None` for a type that the builders leave to `Expr`.
fn map_type(ty: &Type, enums: &mut Enums) -> Option<Mapped> {
    let (csharp, slint) = match ty {
        Type::String => ("string".into(), "String".into()),
        Type::Int32 => ("int".into(), "Int".into()),
        Type::Float32 => ("double".into(), "Float".into()),
        Type::LogicalLength => ("double".into(), "Length".into()),
        Type::Bool => ("bool".into(), "Bool".into()),
        Type::Color => ("global::Slint.Color".into(), "Color".into()),
        Type::Brush => ("global::Slint.Color".into(), "Brush".into()),
        Type::Duration => ("global::System.TimeSpan".into(), "Duration".into()),
        Type::Angle => ("double".into(), "Angle".into()),
        Type::Image => ("global::Slint.Image".into(), "Image".into()),
        Type::Array(item) if **item == Type::String => (
            "global::System.Collections.Generic.IReadOnlyList<string>".into(),
            "StringArray".into(),
        ),
        Type::Struct(s) => {
            let name = s.name.slint_name()?;
            // The structs of the standard widgets, such as `Date`, come from their library; built-in ones need no import.
            let import = match &s.name {
                StructName::User { node, .. } => {
                    if node.source_file.as_ref().is_some_and(|f| f.path().is_builtin()) {
                        format!(", {}", string_literal("std-widgets.slint"))
                    } else {
                        format!(", {}", enums.import)
                    }
                }
                _ => String::new(),
            };
            ("global::Slint.Struct".into(), format!("Struct({}{import})", string_literal(&name)))
        }
        Type::Enumeration(e) => {
            let import =
                if e.node.is_some() { format!(", {}", enums.import) } else { String::new() };
            (enums.csharp(e), format!("Enum({}{import})", string_literal(&e.name)))
        }
        _ => return None,
    };
    Some(Mapped { csharp, slint: format!("global::Slint.Markup.SlintType.{slint}") })
}

fn class_name(name: &str) -> String {
    let class = ident(name);
    if [SLINT_TYPES, IMPLICIT_USING_TYPES, MARKUP_TYPES]
        .iter()
        .any(|types| types.contains(&class.as_str()))
    {
        format!("{class}Element")
    } else {
        class
    }
}

fn enum_name(name: &str) -> String {
    let name = ident(name);
    if SLINT_TYPES.contains(&name.as_str()) { format!("{name}Kind") } else { name }
}

/// The text of a `<summary>`, from a `.slint` doc comment.
fn summary(docs: Option<&str>, fallback: &str) -> String {
    let text = docs.map(str::trim).filter(|d| !d.is_empty()).unwrap_or(fallback);
    let first = text.split("\n\n").next().unwrap_or(text);
    let escaped = first.replace('&', "&amp;").replace('<', "&lt;").replace('>', "&gt;");
    escaped.lines().map(str::trim).join(" ")
}

/// A property, callback, or the `text` constructor of a builder.
struct Member {
    name: SmolStr,
    ty: Type,
    visibility: PropertyVisibility,
    docs: Option<String>,
}

/// Writes the builder methods of `members` into `out`, for the class `this`.
fn write_members(out: &mut String, this: &str, members: &[Member], enums: &mut Enums) {
    let mut used = BTreeSet::new();
    for member in members {
        let mut method = ident(&member.name);
        // C# forbids a member named like its class; the constructors of `Text` cover its `text`,
        // and only the descriptor for Bind remains.
        if method == this {
            if matches!(member.visibility, PropertyVisibility::Input | PropertyVisibility::InOut)
                && let Some(mapped) = map_type(&member.ty, enums)
                && used.insert(format!("{method}Property"))
            {
                let _ = writeln!(
                    out,
                    "    /// <summary>The `{}` property, for <c>Bind</c>.</summary>\n    public static readonly global::Slint.Markup.Property<{}> {method}Property = new({}, {});",
                    member.name,
                    mapped.csharp,
                    string_literal(&member.name),
                    mapped.slint
                );
            }
            continue;
        }
        if ELEMENT_MEMBERS.contains(&method.as_str()) {
            method.push('_');
        }
        let name = string_literal(&member.name);
        match &member.ty {
            Type::Callback(function) if function.return_type == Type::Void => {
                let handler = format!("On{method}");
                if !used.insert(handler.clone()) {
                    continue;
                }
                let doc = summary(member.docs.as_deref(), &format!("Handles `{}`.", member.name));
                let mapped =
                    function.args.iter().map(|a| map_type(a, enums)).collect::<Option<Vec<_>>>();
                match mapped {
                    Some(args) if !args.is_empty() => {
                        let types = args.iter().map(|a| a.csharp.as_str()).join(", ");
                        let slint = args.iter().map(|a| a.slint.as_str()).join(", ");
                        let call = args
                            .iter()
                            .enumerate()
                            .map(|(i, a)| {
                                format!(
                                    "global::Slint.Generated.Conversion.FromSlint<{}>(a[{i}])",
                                    a.csharp
                                )
                            })
                            .join(", ");
                        let _ = writeln!(
                            out,
                            "    /// <summary>{doc} The page updates after the handler.</summary>\n    public {this} {handler}(global::System.Action<{types}> handler) {{ SetHandler({name}, [{slint}], a => handler({call})); return this; }}"
                        );
                        let _ = writeln!(
                            out,
                            "    /// <summary>{doc} The handler doesn't need the arguments; the page updates after it.</summary>\n    public {this} {handler}(global::System.Action handler) {{ SetHandler({name}, [], _ => handler()); return this; }}"
                        );
                    }
                    _ => {
                        let _ = writeln!(
                            out,
                            "    /// <summary>{doc} The page updates after the handler.</summary>\n    public {this} {handler}(global::System.Action handler) {{ SetHandler({name}, [], _ => handler()); return this; }}"
                        );
                    }
                }
                let _ = writeln!(
                    out,
                    "    /// <summary>{doc} The handler receives the item of the row of a ForEach.</summary>\n    public {this} {handler}(global::Slint.Markup.RowAction handler) {{ SetHandler({name}, handler); return this; }}"
                );
                let argument_names = (0..function.args.len())
                    .map(|i| {
                        function
                            .arg_names
                            .get(i)
                            .filter(|n| !n.is_empty())
                            .map_or_else(|| format!("a{i}"), |n| n.to_string())
                    })
                    .collect::<Vec<_>>();
                let names = if argument_names.is_empty() {
                    String::new()
                } else {
                    format!(
                        " The statements name the arguments {}.",
                        argument_names.iter().map(|n| format!("<c>{n}</c>")).join(", ")
                    )
                };
                let arguments = argument_names.iter().map(|n| string_literal(n)).join(", ");
                let _ = writeln!(
                    out,
                    "    /// <summary>{doc} With <c>.slint</c> statements, such as <c>popup.show()</c>, which run without C#.{names}</summary>\n    public {this} {handler}(global::Slint.Markup.Expr statements) {{ SetHandler({name}, [{arguments}], statements); return this; }}"
                );
            }
            Type::Callback(_) | Type::Function(_) => {}
            ty => {
                if !matches!(
                    member.visibility,
                    PropertyVisibility::Input | PropertyVisibility::InOut
                ) || !used.insert(method.clone())
                {
                    continue;
                }
                let Some(mapped) = map_type(ty, enums) else { continue };
                let (csharp, slint) = (&mapped.csharp, &mapped.slint);
                let doc =
                    summary(member.docs.as_deref(), &format!("The `{}` property.", member.name));
                // For Bind of CommunityToolkit.Maui.Markup's kind, such as `.Bind(LineEdit.TextProperty, (Vm vm) => vm.Name)`.
                let descriptor = format!("{method}Property");
                if used.insert(descriptor.clone()) {
                    let _ = writeln!(
                        out,
                        "    /// <summary>The `{}` property, for <c>Bind</c>.</summary>\n    public static readonly global::Slint.Markup.Property<{csharp}> {descriptor} = new({name}, {slint});",
                        member.name
                    );
                }
                let _ = writeln!(
                    out,
                    "    /// <summary>{doc}</summary>\n    public {this} {method}({csharp} value) {{ SetValue({name}, {slint}, value); return this; }}"
                );
                let _ = writeln!(
                    out,
                    "    /// <summary>{doc} The page reads the lambda again on each update.</summary>\n    public {this} {method}(global::System.Func<{csharp}> value) {{ SetBinding({name}, {slint}, () => value()); return this; }}"
                );
                let _ = writeln!(
                    out,
                    "    /// <summary>{doc} From a <c>.slint</c> expression, such as a field of a row.</summary>\n    public {this} {method}(global::Slint.Markup.Expr value) {{ SetExpression({name}, {slint}, value); return this; }}"
                );
                if member.visibility == PropertyVisibility::InOut {
                    let _ = writeln!(
                        out,
                        "    /// <summary>{doc} Binds it both ways: <paramref name=\"set\"/> receives the changes of the UI.</summary>\n    public {this} {method}(global::System.Func<{csharp}> get, global::System.Action<{csharp}> set) {{ SetBinding({name}, {slint}, () => get(), v => set(global::Slint.Generated.Conversion.FromSlint<{csharp}>(v))); return this; }}"
                    );
                    let _ = writeln!(
                        out,
                        "    /// <summary>{doc} From a field of the row of a ForEach, which receives the changes of the UI.</summary>\n    public {this} {method}(global::Slint.Markup.RowBinding<{csharp}> value) {{ SetRowBinding({name}, {slint}, value); return this; }}"
                    );
                }
            }
        }
    }
}

/// The members of a component of the input, without the ones that every element has.
/// The compiler turns an exported component into a window, so its members come from the base
/// that the source names, and from its own public declarations.
fn exported_members(
    component: &Rc<Component>,
    doc: &Document,
    common: &BTreeSet<SmolStr>,
) -> Vec<Member> {
    let base =
        component.node.as_ref().and_then(|node| node.Element().QualifiedName()).and_then(|name| {
            doc.local_registry.lookup_element(name.text().to_string().trim()).ok()
        });
    let root = component.root_element.borrow();
    let own = root
        .property_declarations
        .iter()
        .filter(|(_, d)| d.visibility != PropertyVisibility::Private)
        .map(|(k, d)| Member {
            name: d.declared_name(k).clone(),
            ty: d.property_type.clone(),
            visibility: d.visibility,
            docs: None,
        });
    let inherited = base.iter().flat_map(|base| {
        base.property_list().into_iter().map(move |(name, ty)| {
            let visibility =
                base.lookup_property(&name, PropertyLookupMode::FromOutside).property_visibility;
            Member { name, ty, visibility, docs: None }
        })
    });
    own.chain(inherited.collect::<Vec<_>>())
        .filter(|m| !common.contains(&m.name))
        .unique_by(|m| m.name.clone())
        .sorted_by(|a, b| a.name.cmp(&b.name))
        .collect()
}

/// The members of an element type, without the ones that every element has.
fn members(ty: &ElementType, common: &BTreeSet<SmolStr>) -> Vec<Member> {
    let docs: BTreeMap<SmolStr, Option<String>> = match ty {
        ElementType::Builtin(b) => {
            b.properties.iter().map(|(k, p)| (k.clone(), p.docs.clone())).collect()
        }
        _ => BTreeMap::new(),
    };
    ty.property_list()
        .into_iter()
        .filter(|(name, _)| !common.contains(name))
        .map(|(name, ty_)| {
            let visibility =
                ty.lookup_property(&name, PropertyLookupMode::FromOutside).property_visibility;
            let docs = docs.get(&name).cloned().flatten();
            Member { name, ty: ty_, visibility, docs }
        })
        .sorted_by(|a, b| a.name.cmp(&b.name))
        .collect()
}

/// Writes the builder of `slint_name`, which the page imports from the file of the C# expression `import`,
/// or from nowhere for a built-in element; `extra` goes at the start of the class.
fn write_class(
    out: &mut String,
    slint_name: &str,
    import: &str,
    docs: Option<&str>,
    members: &[Member],
    enums: &mut Enums,
    extra: &str,
) {
    let class = class_name(slint_name);
    let element = string_literal(slint_name);
    let _ = writeln!(
        out,
        "/// <summary>{}</summary>\npublic sealed partial class {class} : global::Slint.Markup.Element<{class}>\n{{{extra}",
        summary(docs, &format!("The `{slint_name}` element."))
    );
    let _ = writeln!(
        out,
        "    /// <summary>A `{slint_name}`.</summary>\n    public {class}() : base({element}, {import}) {{ }}"
    );
    let has_text = members.iter().any(|m| {
        m.name == "text"
            && m.ty == Type::String
            && matches!(m.visibility, PropertyVisibility::Input | PropertyVisibility::InOut)
    });
    if has_text {
        let slint = "global::Slint.Markup.SlintType.String";
        let _ = writeln!(
            out,
            "    /// <summary>A `{slint_name}` with a text that doesn't change.</summary>\n    public {class}(string text) : this() => SetValue(\"text\", {slint}, text);"
        );
        let _ = writeln!(
            out,
            "    /// <summary>A `{slint_name}` with a text that the page reads again on each update.</summary>\n    public {class}(global::System.Func<string> text) : this() => SetBinding(\"text\", {slint}, () => text());"
        );
        let _ = writeln!(
            out,
            "    /// <summary>A `{slint_name}` with a text from a <c>.slint</c> expression, such as a field of a row.</summary>\n    public {class}(global::Slint.Markup.Expr text) : this() => SetExpression(\"text\", {slint}, text);"
        );
        // Like Comet's `new Button("Add", () => count.Value++)`.
        let clickable = members.iter().any(|m| {
            m.name == "clicked"
                && matches!(&m.ty, Type::Callback(f) if f.args.is_empty() && f.return_type == Type::Void)
        });
        if clickable {
            let _ = writeln!(
                out,
                "    /// <summary>A `{slint_name}` with a text that doesn't change, which runs <paramref name=\"clicked\"/> on a click.</summary>\n    public {class}(string text, global::System.Action clicked) : this(text) => OnClicked(clicked);"
            );
            let _ = writeln!(
                out,
                "    /// <summary>A `{slint_name}` with a text that the page reads again on each update, which runs <paramref name=\"clicked\"/> on a click.</summary>\n    public {class}(global::System.Func<string> text, global::System.Action clicked) : this(text) => OnClicked(clicked);"
            );
        }
    }
    let clicked = members.iter().any(|m| {
        m.name == "clicked"
            && matches!(&m.ty, Type::Callback(f) if f.args.is_empty() && f.return_type == Type::Void)
    });
    if clicked {
        let enabled = members.iter().any(|m| m.name == "enabled" && m.ty == Type::Bool);
        let enabled = if enabled { "\"enabled\"" } else { "null" };
        let _ = writeln!(
            out,
            "    /// <summary>Runs a command of the view model on a click, and is enabled while it can run.</summary>\n    public {class} BindCommand<TViewModel>(global::System.Func<TViewModel, global::System.Windows.Input.ICommand> command, global::System.Func<TViewModel, object?>? parameter = null) => global::Slint.Markup.MarkupExtensions.BindCommand(this, command, \"clicked\", {enabled}, parameter);"
        );
    }
    write_members(out, &class, members, enums);
    let _ = writeln!(out, "}}\n");
}

/// Returns the C# code of the builders of the components that `doc` exports;
/// with [`Config::markup_builtins`], those of the built-in elements and the components that `doc` imports instead,
/// with the base class of the builders.
pub fn generate(doc: &Document, config: &Config) -> String {
    // The page imports the components and enums of an application's file from its copy in the application's directory.
    let import = format!(
        "global::Slint.Markup.Element.ApplicationFile({})",
        string_literal(config.runtime_path.as_deref().unwrap_or_default())
    );
    let mut enums = Enums {
        builtins: config.markup_builtins,
        generate: BTreeMap::new(),
        import: import.clone(),
    };
    let mut types = String::new();

    // The members of every element go into the base class.
    let common_members = reserved_properties()
        .map(|(name, ty, visibility)| Member { name: name.into(), ty, visibility, docs: None })
        .sorted_by(|a, b| a.name.cmp(&b.name))
        .collect::<Vec<_>>();
    let common = common_members.iter().map(|m| m.name.clone()).collect::<BTreeSet<_>>();

    if config.markup_builtins {
        let _ = writeln!(
            types,
            "/// <summary>The base of the builders, with the properties that every element has.</summary>\npublic abstract partial class Element<TSelf> where TSelf : Element<TSelf>\n{{"
        );
        write_members(&mut types, "TSelf", &common_members, &mut enums);
        let _ = writeln!(types, "}}\n");
        // The generic base returns `TSelf`; the methods cast once.
        types = types.replace("return this; }", "return (TSelf)this; }");

        let builtins = doc
            .local_registry
            .all_elements()
            .into_iter()
            .filter_map(|(name, ty)| match &ty {
                ElementType::Builtin(b)
                    if !b.is_internal && !b.is_global && !b.is_non_item_type && name == b.name =>
                {
                    Some((name, ty))
                }
                _ => None,
            })
            .sorted_by(|a, b| a.0.cmp(&b.0))
            .collect::<Vec<_>>();
        for (name, ty) in &builtins {
            let ElementType::Builtin(b) = ty else { continue };
            let docs = b.docs.iter().find_map(|d| match d {
                ElementDocEntry::Text(t) => Some(t.as_str()),
                _ => None,
            });
            write_class(&mut types, name, "null", docs, &members(ty, &common), &mut enums, "");
        }

        // The components that the input imports, such as the standard widgets.
        // Exporting them instead would make each a top-level component, which a popup can't be.
        let components = doc
            .local_registry
            .all_elements()
            .into_iter()
            .filter_map(|(name, ty)| match &ty {
                ElementType::Component(c)
                    if !c.is_global()
                        && c.node.as_ref().is_some_and(|n| n.source_file.path().is_builtin()) =>
                {
                    Some((name, ty))
                }
                _ => None,
            })
            .sorted_by(|a, b| a.0.cmp(&b.0))
            .collect::<Vec<_>>();
        for (name, ty) in &components {
            let import = string_literal("std-widgets.slint");
            write_class(&mut types, name, &import, None, &members(ty, &common), &mut enums, "");
        }
    } else {
        let components = doc
            .exports
            .iter()
            .filter_map(|(export, c)| {
                c.as_ref().left().filter(|c| !c.is_global()).map(|c| (export, c))
            })
            .collect::<Vec<_>>();
        // A page with one of the builders exports the file's globals, which C# reaches through the page's instance.
        let globals = doc
            .exports
            .iter()
            .filter(|(_, c)| c.as_ref().left().is_some_and(|c| c.is_global()))
            .map(|(export, _)| string_literal(export.as_str()))
            .collect::<Vec<_>>();
        let globals = if globals.is_empty() {
            String::new()
        } else {
            format!(
                "\n    /// <inheritdoc/>\n    protected override global::System.Collections.Generic.IReadOnlyList<string> ExportedGlobals => [{}];",
                globals.join(", ")
            )
        };
        for (export, component) in components {
            // Each builder registers the enums of this file before its first use.
            let registration = format!(
                "\n    static {}() => MarkupEnums.Register();{globals}",
                class_name(export.as_str())
            );
            write_class(
                &mut types,
                export.as_str(),
                &import,
                None,
                &exported_members(component, doc, &common),
                &mut enums,
                &registration,
            );
        }
    }

    let mut registrations = String::new();
    for e in enums.generate.values() {
        write_enum(&mut types, e, &mut registrations);
    }
    // write_enum names the enum after Slint; a renamed one keeps its C# name.
    for e in enums.generate.values() {
        let slint = ident(&e.name);
        let csharp = enum_name(&e.name);
        if slint != csharp {
            types = types
                .replace(&format!("public enum {slint}\n"), &format!("public enum {csharp}\n"))
                .replace(&format!("Register<{slint}>"), &format!("Register<{csharp}>"))
                .replace(&format!("{slint}."), &format!("{csharp}."));
        }
    }

    let namespace = config.namespace.as_deref().unwrap_or("Slint.Markup");
    let mut out = String::new();
    let _ = writeln!(
        out,
        "// <auto-generated>\n// Generated by slint-compiler -f csharp --csharp-markup. Don't edit.\n// </auto-generated>\n#nullable enable\n#pragma warning disable CS1591\n\nnamespace {namespace};\n"
    );
    if config.markup_builtins {
        let _ = writeln!(
            out,
            "public abstract partial class Element\n{{\n    static Element()\n    {{\n{registrations}    }}\n}}\n"
        );
    } else {
        let _ = writeln!(
            out,
            "file static class MarkupEnums\n{{\n    private static bool s_registered;\n\n    internal static void Register()\n    {{\n        if (s_registered)\n        {{\n            return;\n        }}\n        s_registered = true;\n{registrations}    }}\n}}\n"
        );
    }
    out.push_str(&types);
    out
}
