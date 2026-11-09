// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

//! The builders of `Slint.Markup`, which write `.slint` source from C#:
//! one class for each built-in element and each component that the input exports.

use super::{Config, ident, string_literal, write_enum};
use crate::element_type::{ElementDocEntry, ElementType, PropertyLookupMode};
use crate::langtype::{Enumeration, Type};
use crate::native_class::PropertyVisibility;
use crate::object_tree::Document;
use crate::typeregister::reserved_properties;
use itertools::Itertools;
use smol_str::SmolStr;
use std::collections::{BTreeMap, BTreeSet};
use std::fmt::Write;
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

/// The C# side of `ty`, or `None` for a type that the builders leave to `Expr`.
fn map_type(ty: &Type, enums: &mut BTreeMap<SmolStr, Arc<Enumeration>>) -> Option<Mapped> {
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
        Type::Enumeration(e) => {
            enums.insert(e.name.clone(), e.clone());
            (enum_name(&e.name), format!("Enum({})", string_literal(&e.name)))
        }
        _ => return None,
    };
    Some(Mapped { csharp, slint: format!("global::Slint.Markup.SlintType.{slint}") })
}

fn class_name(name: &str) -> String {
    let class = ident(name);
    if SLINT_TYPES.contains(&class.as_str()) || MARKUP_TYPES.contains(&class.as_str()) {
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
fn write_members(
    out: &mut String,
    this: &str,
    members: &[Member],
    enums: &mut BTreeMap<SmolStr, Arc<Enumeration>>,
) {
    let mut used = BTreeSet::new();
    for member in members {
        let mut method = ident(&member.name);
        // C# forbids a member named like its class; the constructors of `Text` cover its `text`.
        if method == this {
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
                }
            }
        }
    }
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

fn write_class(
    out: &mut String,
    slint_name: &str,
    import: Option<&str>,
    docs: Option<&str>,
    members: &[Member],
    enums: &mut BTreeMap<SmolStr, Arc<Enumeration>>,
) {
    let class = class_name(slint_name);
    let import = import.map_or("null".to_string(), string_literal);
    let element = string_literal(slint_name);
    let _ = writeln!(
        out,
        "/// <summary>{}</summary>\npublic sealed partial class {class} : global::Slint.Markup.Element<{class}>\n{{",
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
    }
    write_members(out, &class, members, enums);
    let _ = writeln!(out, "}}\n");
}

/// Returns the C# code of the builders: the built-in elements, and the components that `doc` imports.
pub fn generate(doc: &Document, config: &Config) -> String {
    let mut enums = BTreeMap::new();
    let mut types = String::new();

    // The members of every element go into the base class.
    let common_members = reserved_properties()
        .map(|(name, ty, visibility)| Member { name: name.into(), ty, visibility, docs: None })
        .sorted_by(|a, b| a.name.cmp(&b.name))
        .collect::<Vec<_>>();
    let common = common_members.iter().map(|m| m.name.clone()).collect::<BTreeSet<_>>();
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
        write_class(&mut types, name, None, docs, &members(ty, &common), &mut enums);
    }

    // The components that the input imports, such as the standard widgets.
    // Exporting them instead would make each a top-level component, which a popup can't be.
    let own_file = config.runtime_path.clone().unwrap_or_default();
    let components = doc
        .local_registry
        .all_elements()
        .into_iter()
        .filter_map(|(name, ty)| match &ty {
            ElementType::Component(c)
                if !c.is_global()
                    && c.node.as_ref().is_none_or(|n| {
                        Some(n.source_file.path())
                            != doc.node.as_ref().map(|d| d.source_file.path())
                    }) =>
            {
                Some((name, c.clone(), ty))
            }
            _ => None,
        })
        .sorted_by(|a, b| a.0.cmp(&b.0))
        .collect::<Vec<_>>();
    for (name, component, ty) in &components {
        // The standard widgets come from the style's library, whatever file imports them.
        let from_style = component.node.as_ref().is_some_and(|n| n.source_file.path().is_builtin());
        let import = if from_style { "std-widgets.slint" } else { own_file.as_str() };
        write_class(&mut types, name, Some(import), None, &members(ty, &common), &mut enums);
    }

    let mut registrations = String::new();
    for e in enums.values() {
        write_enum(&mut types, e, &mut registrations);
    }
    // write_enum names the enum after Slint; a renamed one keeps its C# name.
    for e in enums.values() {
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
    let _ = writeln!(
        out,
        "public abstract partial class Element\n{{\n    static Element()\n    {{\n{registrations}    }}\n}}\n"
    );
    out.push_str(&types);
    out
}
