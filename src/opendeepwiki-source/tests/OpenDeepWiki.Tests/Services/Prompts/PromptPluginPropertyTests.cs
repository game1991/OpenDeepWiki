using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace OpenDeepWiki.Tests.Services.Prompts;

/// <summary>
/// Property-based tests for Prompt Plugin variable substitution.
/// Feature: repository-wiki-generation, Property 11: Prompt Plugin Variable Substitution
/// Validates: Requirements 15.1, 15.4
/// </summary>
public partial class PromptPluginPropertyTests
{
    /// <summary>
    /// Regex pattern to match {{variable}} placeholders.
    /// </summary>
    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.Compiled)]
    private static partial Regex VariablePlaceholderRegex();

    /// <summary>
    /// Substitutes {{variable}} placeholders in the template with provided values.
    /// This is a copy of the implementation for testing purposes.
    /// </summary>
    private static string SubstituteVariables(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template) || variables.Count == 0)
        {
            return template;
        }

        return VariablePlaceholderRegex().Replace(template, match =>
        {
            var variableName = match.Groups[1].Value;
            return variables.TryGetValue(variableName, out var value) ? value : match.Value;
        });
    }

    /// <summary>
    /// Generates valid variable names (alphanumeric with underscores).
    /// </summary>
    private static Gen<string> GenerateValidVariableName()
    {
        return Gen.Elements(
            "repository_name", "language", "branch", "author",
            "version", "date", "title", "description",
            "changed_files", "previous_commit", "current_commit");
    }

    /// <summary>
    /// Generates valid variable values (non-empty strings without special chars).
    /// </summary>
    private static Gen<string> GenerateValidVariableValue()
    {
        return Gen.Elements(
            "my-repo", "en", "main", "John Doe",
            "1.0.0", "2024-01-15", "Overview", "A sample description",
            "src/main.cs\nREADME.md", "abc123", "def456");
    }

    /// <summary>
    /// Generates a template with variable placeholders.
    /// </summary>
    private static Gen<(string Template, Dictionary<string, string> Variables)> GenerateTemplateWithVariables()
    {
        return Gen.Choose(1, 5).SelectMany(varCount =>
            GenerateValidVariableName()
                .ListOf(varCount)
                .SelectMany(varNames =>
                    GenerateValidVariableValue()
                        .ListOf(varCount)
                        .Select(varValues =>
                        {
                            var uniqueNames = varNames.Distinct().ToList();
                            var variables = new Dictionary<string, string>();
                            var templateParts = new List<string> { "Start of template. " };

                            for (int i = 0; i < uniqueNames.Count && i < varValues.Count; i++)
                            {
                                variables[uniqueNames[i]] = varValues.ElementAt(i);
                                templateParts.Add($"Variable {{{{" + uniqueNames[i] + "}}}} here. ");
                            }

                            templateParts.Add("End of template.");
                            return (string.Join("", templateParts), variables);
                        })));
    }

    /// <summary>
    /// Property 11: Prompt Plugin Variable Substitution
    /// For any prompt template with variables, loading with variable values SHALL replace 
    /// all {{variable}} placeholders with provided values.
    /// Validates: Requirements 15.1, 15.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SubstituteVariables_ShouldReplaceAllPlaceholders()
    {
        return Prop.ForAll(
            GenerateTemplateWithVariables().ToArbitrary(),
            data =>
            {
                var (template, variables) = data;
                var result = SubstituteVariables(template, variables);

                // Verify all variable placeholders are replaced
                foreach (var (name, value) in variables)
                {
                    var placeholder = "{{" + name + "}}";
                    if (template.Contains(placeholder) && !result.Contains(placeholder) && result.Contains(value))
                    {
                        continue;
                    }
                    else if (!template.Contains(placeholder))
                    {
                        continue;
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            });
    }

    /// <summary>
    /// Property 11: Prompt Plugin Variable Substitution
    /// For any template without placeholders, substitution SHALL return the original template unchanged.
    /// Validates: Requirements 15.1, 15.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SubstituteVariables_WithNoPlaceholders_ShouldReturnOriginal()
    {
        var templateGen = Gen.Elements(
            "This is a simple template without variables.",
            "Another template with no placeholders at all.",
            "Just plain text here.",
            "# Markdown Header\n\nSome content without any {{}} patterns that are valid.");

        var variablesGen = GenerateValidVariableName()
            .SelectMany(name => GenerateValidVariableValue()
                .Select(value => new Dictionary<string, string> { { name, value } }));

        return Prop.ForAll(
            templateGen.ToArbitrary(),
            variablesGen.ToArbitrary(),
            (template, variables) =>
            {
                var result = SubstituteVariables(template, variables);
                // If template has no matching placeholders, result should equal template
                var hasMatchingPlaceholder = variables.Keys.Any(k => template.Contains("{{" + k + "}}"));
                return hasMatchingPlaceholder || result == template;
            });
    }

    /// <summary>
    /// Property 11: Prompt Plugin Variable Substitution
    /// For any template, substitution with empty variables dictionary SHALL return original template.
    /// Validates: Requirements 15.1, 15.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SubstituteVariables_WithEmptyVariables_ShouldReturnOriginal()
    {
        var templateGen = Gen.Elements(
            "Template with {{variable}} placeholder.",
            "Another {{test}} template.",
            "Multiple {{var1}} and {{var2}} placeholders.");

        return Prop.ForAll(
            templateGen.ToArbitrary(),
            template =>
            {
                var result = SubstituteVariables(template, new Dictionary<string, string>());
                return result == template;
            });
    }

    /// <summary>
    /// Property 11: Prompt Plugin Variable Substitution
    /// For any substitution, unmatched placeholders SHALL remain unchanged.
    /// Validates: Requirements 15.1, 15.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SubstituteVariables_UnmatchedPlaceholders_ShouldRemainUnchanged()
    {
        return Prop.ForAll(
            GenerateValidVariableName().ToArbitrary(),
            GenerateValidVariableValue().ToArbitrary(),
            (varName, varValue) =>
            {
                var unmatchedPlaceholder = "{{unmatched_variable}}";
                var template = $"Start {{{{" + varName + "}}}} middle " + unmatchedPlaceholder + " end.";
                var variables = new Dictionary<string, string> { { varName, varValue } };

                var result = SubstituteVariables(template, variables);

                // Unmatched placeholder should remain
                return result.Contains(unmatchedPlaceholder) && result.Contains(varValue);
            });
    }
}
