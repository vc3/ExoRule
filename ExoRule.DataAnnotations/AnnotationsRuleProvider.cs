﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using ExoGraph;
using System.Text.RegularExpressions;
using ExoRule.Validation;

namespace ExoRule.DataAnnotations
{
	public class AnnotationsRuleProvider : IRuleProvider
	{
		#region Fields

		static Regex labelRegex = new Regex(@"(^[a-z]+|[A-Z]{2,}(?=[A-Z][a-z]|$)|[A-Z][a-z]*)", RegexOptions.Singleline | RegexOptions.Compiled);

		List<Rule> rules = new List<Rule>();

		#endregion

		#region Constructors

		/// <summary>
		/// Automatically creates property validation rules for the specified <see cref="GraphType"/> instances
		/// based on data annotation attributes associated with properties declared on each type.
		/// </summary>
		/// <param name="types"></param>
		public AnnotationsRuleProvider(IEnumerable<GraphType> types)
		{
			// Process each type
			foreach (var type in types)
			{
				// Process each instance property declared on the current type
				foreach (var property in type.Properties.Where(property => property.DeclaringType == type && !property.IsStatic))
				{
					// Get the display label to use for validation error messages
					Func<string> label = GetLabel(property);

					// Required Attribute
					foreach (var attr in property.GetAttributes<RequiredAttribute>().Take(1))
						rules.Add(new RequiredRule(type.Name, property.Name, label));

					// String Length Attribute
					foreach (var attr in property.GetAttributes<StringLengthAttribute>().Take(1))
						rules.Add(new StringLengthRule(type.Name, property.Name, attr.MinimumLength, attr.MaximumLength, label, (c) => c.ToString()));

					// Range Attribute
					foreach (var attr in property.GetAttributes<RangeAttribute>().Take(1))
						rules.Add(new RangeRule(type.Name, property.Name, (IComparable)attr.Minimum, (IComparable)attr.Maximum, label, GetFormat<IComparable>(property)));

					// Allowed Values Attribute
					GraphReferenceProperty reference = property as GraphReferenceProperty;
					if (reference != null)
					{
						foreach (var source in property.GetAttributes<AllowedValuesAttribute>()
							.Select(attr => attr.Source)
							.Union(reference.PropertyType.GetAttributes<AllowedValuesAttribute>()
							.Select(attr => attr.Source.Contains('.') ? attr.Source : reference.PropertyType.Name + '.' + attr.Source)
							.Take(1)))
							rules.Add(new AllowedValuesRule(type.Name, property.Name, source, label));
					}
				}
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Gets a delegate to return the label of the specified property.
		/// </summary>
		/// <param name="property"></param>
		/// <returns></returns>
		Func<string> GetLabel(GraphProperty property)
		{
			DisplayAttribute displayAttribute = property.GetAttributes<DisplayAttribute>().FirstOrDefault();
			string defaultLabel = labelRegex.Replace(property.Name, " $1").Substring(1);
			return displayAttribute != null ? (Func<string>)(() => displayAttribute.GetName()) : () => defaultLabel;
		}

		/// <summary>
		/// Gets a delagate to format the value of the specified property.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="property"></param>
		/// <returns></returns>
		Func<T, string> GetFormat<T>(GraphProperty property)
		{
			DisplayFormatAttribute formatAttribute = property.GetAttributes<DisplayFormatAttribute>().FirstOrDefault();
			return formatAttribute != null && !String.IsNullOrWhiteSpace(formatAttribute.DataFormatString) ?
				(Func<T, string>)((v) => formatAttribute.DataFormatString) : (v) => v.ToString();
		}

		/// <summary>
		/// Gets the set of precondition rules created by the provider.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		IEnumerable<Rule> IRuleProvider.GetRules(Type sourceType, string name)
		{
			return rules;
		}

		#endregion
	}
}