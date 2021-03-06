﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;
using System.Runtime.Serialization;
using System.Resources;
using System.Linq.Expressions;

namespace ExoRule
{
	/// <summary>
	/// Represents a discrete type of condition that could occur within an application, 
	/// such as a specific error, warning, status, permission, etc., that is information
	/// relevant to an instance in a model, but not part of the domain data for the model.
	/// </summary>
	[Serializable]
	public abstract class ConditionType : IRuleProvider
	{
		#region Fields

		static IEnumerable<ConditionType> Empty = new List<ConditionType>();
		static Dictionary<string, ConditionType> conditionTypes = new Dictionary<string, ConditionType>();
		static Dictionary<Type, ResourceManager> resources = new Dictionary<Type, ResourceManager>();

		string message;
		Func<string, string> translator;

		#endregion

		#region Constructors

		protected ConditionType(string code, ConditionCategory category, string message, Type sourceType, Func<string, string> translator, params ConditionTypeSet[] sets)
			: this(code, category, message, sets)
		{
			ResourceManager resource;
			if (resources.TryGetValue(sourceType, out resource))
			{
				// Create a translator to first look up the resource
				this.translator = (s) =>
				{
					// Look up the resource
					s = resource.GetString(s) ?? s;

					// Perform additional transation
					if (translator != null)
						s = translator(s);

					// Return the translated string
					return s;
				};
			}
			else
				this.translator = translator;
		}

		protected ConditionType(string code, ConditionCategory category, string message, params ConditionTypeSet[] sets)
		{
			this.Sets = sets;
			this.Code = code;
			this.Category = category;
			this.Message = message;
		}

		#endregion

		#region Properties

		public string Code { get; internal set; }
	
		public ConditionCategory Category { get; private set; }

		public IEnumerable<ConditionTypeSet> Sets { get; private set; }

		public string Message
		{
			get
			{
				return translator != null ? translator(message) : message;
			}
			private set
			{
				message = value;
			}
		}

		public Rule ConditionRule { get; private set; }

		public bool AlwaysSerialize { get; set; }

		#endregion

		#region Methods

		/// <summary>
		/// Allows subclasses to create an condition rule based on a concrete condition predicate.
		/// </summary>
		/// <typeparam name="TRoot"></typeparam>
		/// <param name="condition"></param>
		/// <param name="predicates"></param>
		/// <param name="properties"></param>
		protected void CreateConditionRule<TRoot>(Expression<Func<TRoot, bool>> condition, params string[] properties)
			where TRoot : class
		{
			// Creates the condition rule based on the specified condition expression
			this.ConditionRule = new Rule<TRoot>.Condition(this, condition, properties);
		}

		/// <summary>
		/// Allows subclasses to create an condition rule based on a concrete condition predicate.
		/// </summary>
		/// <typeparam name="TRoot"></typeparam>
		/// <param name="condition"></param>
		/// <param name="predicates"></param>
		/// <param name="properties"></param>
		protected void CreateConditionRule<TRoot>(Func<TRoot, bool> condition, string[] predicates, string[] properties)
			where TRoot : class
		{
			// Automatically calculate predicates if they were not specified
			if (predicates == null)
				predicates = PredicateBuilder.GetPredicates(condition.Method, method => Rule<TRoot>.PredicateFilter(condition.Method, method), false).ToArray();

			// Automatically default the properties to be the same as the predicates if they are not specified.
			if (properties == null)
				properties = predicates;

			// Create an condition rule based on the specified condition
			this.ConditionRule = new Rule<TRoot>(RuleInvocationType.PropertyChanged, predicates, new ConditionType[] { this }, root => When(root, () => condition(root), properties));
		}

		public override string ToString()
		{
			return Code + ": " + Message;
		}

		public override bool Equals(object obj)
		{
			return obj is ConditionType && ((ConditionType)obj).Code == Code;
		}

		public override int GetHashCode()
		{
			return Code.GetHashCode();
		}

		public static bool operator ==(ConditionType e1, ConditionType e2)
		{
			if ((object)e1 == null)
				return (object)e2 == null;
			else
				return e1.Equals(e2);
		}

		public static bool operator !=(ConditionType e1, ConditionType e2)
		{
			return !(e1 == e2);
		}

		public static implicit operator ConditionType(string errorCode)
		{
			ConditionType error;
			conditionTypes.TryGetValue(errorCode, out error);
			return error;
		}

		/// <summary>
		/// Directly invokes the condition rule associated with the current condition type instance.
		/// </summary>
		/// <param name="target"></param>
		/// <returns></returns>
		public void When(object target)
		{
			if (ConditionRule == null)
				throw new NotSupportedException("The current condition type, " + Code + ", does not have an associated condition rule.");

			ConditionRule.Invoke(ModelContext.Current.GetModelType(target).GetModelInstance(target), null);
		}

		/// <summary>
		/// Creates or removes an condition on the specified target based on the specified condition.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="condition"></param>
		/// <param name="properties"></param>
		public Condition When(object target, Func<bool> condition, params string[] properties)
		{
			return When(Message, target, condition, properties);
		}

		/// <summary>
		/// Creates or removes an condition on the specified target based on the specified condition.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="condition"></param>
		/// <param name="properties"></param>
		public Condition When(string message, object target, Func<bool> condition, params string[] properties)
		{
			// Get the current condition if it exists
			ConditionTarget conditionTarget = ModelInstance.GetModelInstance(target).GetExtension<RuleManager>().GetCondition(this);

			// Add the condition on the target if it does not exist yet
			if (condition())
			{
				// Create a new condition if one does not exist
				if (conditionTarget == null)
					return new Condition(this, message, target, properties);

				// Return the existing condition
				else
				{
					// Update the message if a different message was passed in
					if (!string.IsNullOrEmpty(message) && message != conditionTarget.Condition.Message)
						conditionTarget.Condition.Message = message;

					return conditionTarget.Condition;
				}
			}

			// Destroy the condition if it exists on the target and is no longer valid
			if (conditionTarget != null)
				conditionTarget.Condition.Destroy();

			// Return null to indicate that no condition was created
			return null;
		}

		/// <summary>
		/// Gets condition types associated with rules that are registered for the specified <see cref="ModelType"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static IEnumerable<ConditionType> GetConditionTypes(ModelType type)
		{
			return type.GetExtension<HashSet<ConditionType>>();
		}

		/// <summary>
		/// Maps resources for the specified source types to the specified resource type,
		/// which will cause all condition types tied to one of the mapped source types
		/// to load resources using an alternate resource type.
		/// </summary>
		/// <param name="sourceTypes"></param>
		/// <param name="resourceType"></param>
		public static void MapResources(IEnumerable<Type> sourceTypes, Type resourceType)
		{
			ResourceManager resource;
			if (!resources.TryGetValue(resourceType, out resource))
				resources[resourceType] = resource = new ResourceManager(resourceType);
			foreach (Type sourceType in sourceTypes)
				resources[sourceType] = resource;
		}

		/// <summary>
		/// Creates a translation based on the specified source type used for resource lookup
		/// and the optional translation function.
		/// </summary>
		/// <param name="sourceType"></param>
		/// <param name="translator"></param>
		Func<string, string> GetTranslation(Type sourceType, Func<string, string> translator)
		{
			// First determine if resources have been mapped for the specified source type.
			ResourceManager resource;
			if (resources.TryGetValue(sourceType, out resource))
			{
				// Create a translator to first look up the resource
				return (s) =>
				{
					// Look up the resource
					s = resource.GetString(s) ?? s;

					// Perform additional transation
					if (translator != null)
						s = translator(s);

					// Return the translated string
					return s;
				};
			}
			else
				return translator;
		}

		#endregion

		#region IRuleProvider Members

		/// <summary>
		/// Returns the condition rule associated with the current condition type instance.
		/// </summary>
		/// <returns></returns>
		IEnumerable<Rule> IRuleProvider.GetRules(Type sourceType, string name)
		{
			// Initialize resource translation support if a translator has not been assigned
			if (this.translator == null)
			{
				ResourceManager resource;
				if (resources.TryGetValue(sourceType, out resource))
					this.translator = (s) => resource.GetString(s) ?? s;
			}

			// Initialize the code of the condition type if it has not already been set
			if (this.Code == null)
				this.Code = sourceType.Name + "." + name;

			// Return the condition rule if defined
			if (ConditionRule != null)
			{
				foreach (Rule rule in ((IRuleProvider)ConditionRule).GetRules(sourceType, name))
					yield return rule;
			}
		}

		#endregion
	}
}
