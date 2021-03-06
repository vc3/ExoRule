﻿using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using ExoModel;
using ExoRule;

namespace ExoRule.Validation
{
	/// <summary>
	/// Applies conditions to a <see cref="ModelProperty"/> when specific validation conditions occur.
	/// </summary>
	public class ValidationRule : PropertyRule
	{
		#region Fields

		string validationExpr;
		string errorMessageExpr;
        bool isResourceErrorMessage;
		ModelExpression validationExpression;
		ModelExpression errorMessageExpression;

		#endregion

		#region Constructors

		public ValidationRule(string rootType, string property, string errorName, string validationExpression, string errorMessageExpressionOrResource, bool isResourceErrorMessage = false)
			: base(rootType, property, new Error(GetErrorCode(rootType, property, errorName), "Invalid", null), RuleInvocationType.InitNew | RuleInvocationType.PropertyChanged, property)
		{
			this.validationExpr = validationExpression;
            if (isResourceErrorMessage)
                this.ErrorMessageResource = errorMessageExpressionOrResource;
            else
                this.errorMessageExpr = errorMessageExpressionOrResource;

            Initialize += (s, e) => InitializeExpressions();
		}

		public ValidationRule(string rootType, string property, string errorName, string[] additionalPredicates, ModelExpression validationExpression, ModelExpression errorMessageExpression)
			: base(rootType, property, new Error(GetErrorCode(rootType, property, errorName), "Invalid", null), RuleInvocationType.InitNew | RuleInvocationType.PropertyChanged, new string[] { property }.Concat(additionalPredicates).ToArray())
		{
			this.validationExpression = validationExpression;
			this.errorMessageExpression = errorMessageExpression;

			Initialize += (s, e) => InitializeExpressions();
		}

		public ValidationRule(string rootType, string property, string errorName, ModelExpression validationExpression, ModelExpression errorMessageExpression)
			: base(rootType, property, new Error(GetErrorCode(rootType, property, errorName), "Invalid", null), RuleInvocationType.InitNew | RuleInvocationType.PropertyChanged, property)
		{
			this.validationExpression = validationExpression;
			this.errorMessageExpression = errorMessageExpression;

			Initialize += (s, e) => InitializeExpressions();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the <see cref="ModelExpression"/> for the minimum valid value.
		/// </summary>
		public ModelExpression ValidationExpression
		{
			get
			{
				return validationExpression ?? (validationExpr != null ? RootType.GetExpression<bool>(validationExpr) : null);
			}
		}

		/// <summary>
		/// Gets the <see cref="ModelExpression"/> for the maximum valid value.
		/// </summary>
		public ModelExpression ErrorMessageExpression
		{
			get
			{
				return errorMessageExpression ?? (errorMessageExpr != null ? RootType.GetExpression<string>(errorMessageExpr) : null);
			}
		}

        public string ErrorMessageResource { get; set; }

		public string Path { get; private set; }

        public string[] AdditionalTargets { get; set; }

		#endregion

		#region Methods

		void InitializeExpressions()
		{
			var validationPath = ValidationExpression.Path.Path;
			var errorMessagePath = (ErrorMessageResource != null ? "" : ErrorMessageExpression.Path.Path);
			if (!string.IsNullOrEmpty(validationPath) || !string.IsNullOrEmpty(errorMessagePath))
			{
				validationPath = validationPath.StartsWith("{") ? validationPath.Substring(1, validationPath.Length - 2) : validationPath;
				errorMessagePath = errorMessagePath.StartsWith("{") ? errorMessagePath.Substring(1, errorMessagePath.Length - 2) : errorMessagePath;
				Path = "{" + (validationPath.Length > 0 && errorMessagePath.Length > 0 ? validationPath + "," + errorMessagePath : validationPath.Length > 0 ? validationPath : errorMessagePath) + "}";
				SetPredicates(Property.Name, Path);
			}
		}

		protected override bool ConditionApplies(ModelInstance root)
		{
			return (bool)ValidationExpression.Invoke(root);
		}

		#endregion
	}
}
