using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models.Modernization;
using System.Text;

namespace NetLift.Transforms.Modernization.Generators;

/// <summary>
/// Generates FluentValidation validators for CQRS Commands and Queries.
/// </summary>
public sealed class ValidatorGenerator : IValidatorGenerator
{
    private const string Indent = "    ";
    private const string DoubleIndent = "        ";
    private const string TripleIndent = "            ";

    /// <summary>
    /// Generates a FluentValidation validator class.
    /// </summary>
    /// <param name="validatorInfo">Information about the validator to generate</param>
    /// <returns>Generated C# source code for the validator class</returns>
    public string Generate(ValidatorInfo validatorInfo)
    {
        ArgumentNullException.ThrowIfNull(validatorInfo);

        var sb = new StringBuilder();

        // Add namespace
        sb.AppendLine($"namespace {validatorInfo.Namespace};");
        sb.AppendLine();

        // Add usings
        sb.AppendLine("using FluentValidation;");
        sb.AppendLine();

        // Add XML documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Validator for {validatorInfo.ValidatedType}.");

        if (validatorInfo.Confidence < 100)
        {
            sb.AppendLine($"/// Generated with {validatorInfo.Confidence}% confidence.");
        }

        if (!string.IsNullOrWhiteSpace(validatorInfo.SourceReference))
        {
            sb.AppendLine($"/// Source: {validatorInfo.SourceReference}");
        }

        sb.AppendLine("/// </summary>");

        // Generate validator class
        sb.AppendLine($"public sealed class {validatorInfo.Name} : AbstractValidator<{validatorInfo.ValidatedType}>");
        sb.AppendLine("{");

        // Add constructor
        sb.AppendLine($"{Indent}public {validatorInfo.Name}()");
        sb.AppendLine($"{Indent}{{");

        // Group rules by property
        var rulesByProperty = validatorInfo.Rules
            .GroupBy(r => r.PropertyName)
            .OrderBy(g => g.Key);

        bool firstProperty = true;
        foreach (var propertyGroup in rulesByProperty)
        {
            if (!firstProperty)
            {
                sb.AppendLine();
            }
            firstProperty = false;

            GeneratePropertyRules(sb, propertyGroup.Key, propertyGroup.ToList());
        }

        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a validator for a command.
    /// </summary>
    /// <param name="commandInfo">Information about the command</param>
    /// <returns>Generated C# source code for the validator class</returns>
    public string GenerateForCommand(CommandInfo commandInfo)
    {
        ArgumentNullException.ThrowIfNull(commandInfo);

        if (!commandInfo.RequiresValidation)
        {
            return string.Empty;
        }

        // Build validator info from command properties
        var validatorInfo = new ValidatorInfo
        {
            Name = GetValidatorName(commandInfo.Name),
            Namespace = commandInfo.Namespace,
            ValidatedType = commandInfo.Name,
            Rules = ExtractRulesFromProperties(commandInfo.Properties),
            Confidence = commandInfo.Confidence,
            SourceReference = $"{commandInfo.Source.ControllerName}.{commandInfo.Source.ActionName}"
        };

        return Generate(validatorInfo);
    }

    /// <summary>
    /// Generates a validator for a query.
    /// </summary>
    /// <param name="queryInfo">Information about the query</param>
    /// <returns>Generated C# source code for the validator class</returns>
    public string GenerateForQuery(QueryInfo queryInfo)
    {
        ArgumentNullException.ThrowIfNull(queryInfo);

        if (!queryInfo.RequiresValidation)
        {
            return string.Empty;
        }

        // Build validator info from query properties
        var rules = new List<ValidationRule>();

        // Add pagination validation if supported
        if (queryInfo.SupportsPagination)
        {
            var pageNumberProp = queryInfo.Properties.FirstOrDefault(p =>
                p.Name.Equals("PageNumber", StringComparison.OrdinalIgnoreCase));
            var pageSizeProp = queryInfo.Properties.FirstOrDefault(p =>
                p.Name.Equals("PageSize", StringComparison.OrdinalIgnoreCase));

            if (pageNumberProp != null)
            {
                rules.Add(new ValidationRule
                {
                    PropertyName = pageNumberProp.Name,
                    ValidationMethod = "GreaterThan",
                    Parameters = new[] { "0" }
                });
            }

            if (pageSizeProp != null)
            {
                rules.Add(new ValidationRule
                {
                    PropertyName = pageSizeProp.Name,
                    ValidationMethod = "GreaterThan",
                    Parameters = new[] { "0" }
                });
                rules.Add(new ValidationRule
                {
                    PropertyName = pageSizeProp.Name,
                    ValidationMethod = "LessThanOrEqualTo",
                    Parameters = new[] { "100" },
                    ErrorMessage = "Page size cannot exceed 100 items"
                });
            }
        }

        // Add validation rules from properties
        rules.AddRange(ExtractRulesFromProperties(queryInfo.Properties));

        var validatorInfo = new ValidatorInfo
        {
            Name = GetValidatorName(queryInfo.Name),
            Namespace = queryInfo.Namespace,
            ValidatedType = queryInfo.Name,
            Rules = rules,
            Confidence = queryInfo.Confidence,
            SourceReference = $"{queryInfo.Source.ControllerName}.{queryInfo.Source.ActionName}"
        };

        return Generate(validatorInfo);
    }

    private static void GeneratePropertyRules(StringBuilder sb, string propertyName, List<ValidationRule> rules)
    {
        if (rules.Count == 0)
        {
            return;
        }

        // Start RuleFor
        sb.Append($"{DoubleIndent}RuleFor(x => x.{propertyName})");

        // Group rules by whether they need conditional execution
        var unconditionalRules = new List<ValidationRule>();
        var conditionalRules = new List<ValidationRule>();

        foreach (var rule in rules)
        {
            if (ShouldBeConditional(rule))
            {
                conditionalRules.Add(rule);
            }
            else
            {
                unconditionalRules.Add(rule);
            }
        }

        // Add unconditional rules
        foreach (var rule in unconditionalRules)
        {
            sb.AppendLine();
            sb.Append($"{TripleIndent}.{GenerateValidationMethodCall(rule)}");
        }

        // Add conditional rules (these need When clause)
        foreach (var rule in conditionalRules)
        {
            sb.AppendLine();
            sb.Append($"{TripleIndent}.{GenerateValidationMethodCall(rule)}");
            sb.AppendLine();
            sb.Append($"{TripleIndent}.When(x => !string.IsNullOrEmpty(x.{propertyName}))");
        }

        sb.AppendLine(";");
    }

    private static string GenerateValidationMethodCall(ValidationRule rule)
    {
        var sb = new StringBuilder();
        sb.Append(rule.ValidationMethod);
        sb.Append('(');

        // Add parameters
        if (rule.Parameters.Count > 0)
        {
            sb.Append(string.Join(", ", rule.Parameters));
        }

        sb.Append(')');

        // Add custom error message if provided
        if (!string.IsNullOrWhiteSpace(rule.ErrorMessage))
        {
            sb.Append($".WithMessage(\"{rule.ErrorMessage}\")");
        }

        return sb.ToString();
    }

    private static bool ShouldBeConditional(ValidationRule rule)
    {
        // EmailAddress and RegularExpression should be conditional to avoid false positives on null/empty strings
        return rule.ValidationMethod is "EmailAddress" or "Matches" or "Must";
    }

    private static string GetValidatorName(string typeName)
    {
        // Remove "Command" or "Query" suffix if present
        var name = typeName;

        if (name.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^7];
        }
        else if (name.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^5];
        }

        return $"{name}Validator";
    }

    private static List<ValidationRule> ExtractRulesFromProperties(IReadOnlyList<CommandProperty> properties)
    {
        var rules = new List<ValidationRule>();

        foreach (var property in properties)
        {
            // Convert validation rules from property
            foreach (var validationRule in property.ValidationRules)
            {
                var parsedRules = ParseValidationRule(property.Name, validationRule);
                rules.AddRange(parsedRules);
            }

            // Add basic required validation if property is required and not nullable
            if (property.IsRequired && !property.IsNullable)
            {
                // Check if there's already a NotEmpty rule
                if (!rules.Any(r => r.PropertyName == property.Name && r.ValidationMethod == "NotEmpty"))
                {
                    rules.Add(new ValidationRule
                    {
                        PropertyName = property.Name,
                        ValidationMethod = "NotEmpty"
                    });
                }
            }
        }

        return rules;
    }

    private static List<ValidationRule> ParseValidationRule(string propertyName, string validationRule)
    {
        var rules = new List<ValidationRule>();

        // Parse Data Annotation style rules
        // Format examples: "Required", "StringLength(50)", "StringLength(50, MinimumLength=2)", "Range(0, 150)"

        if (validationRule.Equals("Required", StringComparison.OrdinalIgnoreCase))
        {
            rules.Add(new ValidationRule
            {
                PropertyName = propertyName,
                ValidationMethod = "NotEmpty"
            });
        }
        else if (validationRule.StartsWith("StringLength", StringComparison.OrdinalIgnoreCase))
        {
            var parameters = ExtractParameters(validationRule);
            if (parameters.Count > 0)
            {
                // Maximum length (first parameter)
                rules.Add(new ValidationRule
                {
                    PropertyName = propertyName,
                    ValidationMethod = "MaximumLength",
                    Parameters = new[] { parameters[0] }
                });

                // Minimum length (named parameter)
                var minLength = ExtractNamedParameter(validationRule, "MinimumLength");
                if (!string.IsNullOrEmpty(minLength))
                {
                    rules.Add(new ValidationRule
                    {
                        PropertyName = propertyName,
                        ValidationMethod = "MinimumLength",
                        Parameters = new[] { minLength }
                    });
                }
            }
        }
        else if (validationRule.StartsWith("Range", StringComparison.OrdinalIgnoreCase))
        {
            var parameters = ExtractParameters(validationRule);
            if (parameters.Count >= 2)
            {
                rules.Add(new ValidationRule
                {
                    PropertyName = propertyName,
                    ValidationMethod = "InclusiveBetween",
                    Parameters = new[] { parameters[0], parameters[1] }
                });
            }
        }
        else if (validationRule.Equals("EmailAddress", StringComparison.OrdinalIgnoreCase))
        {
            rules.Add(new ValidationRule
            {
                PropertyName = propertyName,
                ValidationMethod = "EmailAddress"
            });
        }
        else if (validationRule.StartsWith("RegularExpression", StringComparison.OrdinalIgnoreCase))
        {
            var pattern = ExtractStringParameter(validationRule);
            if (!string.IsNullOrEmpty(pattern))
            {
                rules.Add(new ValidationRule
                {
                    PropertyName = propertyName,
                    ValidationMethod = "Matches",
                    Parameters = new[] { $"@\"{pattern}\"" }
                });
            }
        }
        else if (validationRule.StartsWith("Compare", StringComparison.OrdinalIgnoreCase))
        {
            var otherProperty = ExtractStringParameter(validationRule);
            if (!string.IsNullOrEmpty(otherProperty))
            {
                rules.Add(new ValidationRule
                {
                    PropertyName = propertyName,
                    ValidationMethod = "Equal",
                    Parameters = new[] { $"x => x.{otherProperty}" }
                });
            }
        }
        else if (validationRule.Equals("Url", StringComparison.OrdinalIgnoreCase))
        {
            rules.Add(new ValidationRule
            {
                PropertyName = propertyName,
                ValidationMethod = "Must",
                Parameters = new[] { "BeValidUrl" },
                ErrorMessage = "Must be a valid URL"
            });
        }
        else if (validationRule.StartsWith("MinLength", StringComparison.OrdinalIgnoreCase))
        {
            var parameters = ExtractParameters(validationRule);
            if (parameters.Count > 0)
            {
                rules.Add(new ValidationRule
                {
                    PropertyName = propertyName,
                    ValidationMethod = "MinimumLength",
                    Parameters = new[] { parameters[0] }
                });
            }
        }
        else if (validationRule.StartsWith("MaxLength", StringComparison.OrdinalIgnoreCase))
        {
            var parameters = ExtractParameters(validationRule);
            if (parameters.Count > 0)
            {
                rules.Add(new ValidationRule
                {
                    PropertyName = propertyName,
                    ValidationMethod = "MaximumLength",
                    Parameters = new[] { parameters[0] }
                });
            }
        }

        return rules;
    }

    private static List<string> ExtractParameters(string rule)
    {
        var parameters = new List<string>();

        var startIndex = rule.IndexOf('(');
        var endIndex = rule.LastIndexOf(')');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            var paramString = rule.Substring(startIndex + 1, endIndex - startIndex - 1);
            var parts = paramString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var part in parts)
            {
                // Skip named parameters (e.g., "MinimumLength=2")
                if (!part.Contains('='))
                {
                    parameters.Add(part.Trim());
                }
            }
        }

        return parameters;
    }

    private static string? ExtractNamedParameter(string rule, string parameterName)
    {
        var startIndex = rule.IndexOf('(');
        var endIndex = rule.LastIndexOf(')');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            var paramString = rule.Substring(startIndex + 1, endIndex - startIndex - 1);
            var parts = paramString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var part in parts)
            {
                if (part.StartsWith($"{parameterName}=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = part.Substring(parameterName.Length + 1).Trim();
                    return value;
                }
            }
        }

        return null;
    }

    private static string? ExtractStringParameter(string rule)
    {
        var startIndex = rule.IndexOf('(');
        var endIndex = rule.LastIndexOf(')');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            var paramString = rule.Substring(startIndex + 1, endIndex - startIndex - 1);
            // Remove quotes if present
            return paramString.Trim().Trim('"', '@', '\'');
        }

        return null;
    }
}
