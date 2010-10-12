﻿using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using ExoGraph;
using ExoRule;

namespace ExoRule.Validation
{
	/// <summary>
	/// Applies conditions when the value of a <see cref="GraphProperty"/> is
	/// null or an empty list.
	/// </summary>
	[DataContract(Name = "required")]
	public class RequiredRule : PropertyRule
	{
		#region Constructors

		public RequiredRule(string rootType, string property, Func<string> label)
			: this(rootType, property, label, RuleInvocationType.PropertyChanged)
		{ }

		protected RequiredRule(string rootType, string property, Func<string> label, RuleInvocationType invocationTypes)
			: base(rootType, property, CreateError(rootType, property, label), invocationTypes, property)
		{ }

		#endregion

		#region Methods

		static Error CreateError(string rootType, string property, Func<string> label)
		{
			return new Error(
				GetErrorCode(rootType, property, "Required"),
				"required",	typeof(RequiredRule), (s) => s.Replace("{property}", label()), null);
		}

		/// <summary>
		/// Determines whether the rule should attach its condition to the given <see cref="GraphInstance"/>.
		/// </summary>
		/// <param name="root">The graph instance to evaluate the rule for.</param>
		/// <returns>A boolean value indicating whether the state of the given <see cref="GraphInstance"/> violates the rule.</returns>
		protected override bool ConditionApplies(GraphInstance root)
		{
			return 
				root[Property] == null || 
				(Property is GraphReferenceProperty && Property.IsList && root.GetList((GraphReferenceProperty)Property).Count == 0);
		}

		protected override string TypeName
		{
			get
			{
				return "required";
			}
		}

		#endregion
	}
}
