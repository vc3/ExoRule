using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections;
using ExoModel;

namespace ExoRule
{
	#region Condition

	/// <summary>
	/// Represents an condition established based on a specific <see cref="ConditionType"/> instance.
	/// </summary>
	public class Condition
	{
		#region Fields

		List<ConditionTarget> targets = new List<ConditionTarget>();

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="Condition"/> based on the specified <see cref="ConditionType"/>.
		/// </summary>
		/// <param name="error"></param>
		public Condition(ConditionType type, object target, params string[] properties)
			: this(type, type.Message, target, properties)
		{ }

		/// <summary>
		/// Creates a new <see cref="Condition"/> based on the specified <see cref="ConditionType"/>
		/// and error message.
		/// </summary>
		/// <param name="error"></param>
		public Condition(ConditionType type, string message, object target, params string[] properties)
		{
			this.Type = type;
			this.Message = message;
			this.AddTarget(target, properties);
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the <see cref="Message"/> for the condition.
		/// </summary>
		public string Message { get; internal set; }

		/// <summary>
		/// Gets the <see cref="ConditionType"/> the condition is for.
		/// </summary>
		public ConditionType Type { get; private set; }

		/// <summary>
		/// Gets the targets of the condition.
		/// </summary>
		public IEnumerable<ConditionTarget> Targets
		{
			get
			{
				return targets;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Adds an additional target to the condition.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="properties"></param>
		public void AddTarget(object target, params string[] properties)
		{
			// Get the root target instance
			ModelInstance root = ModelInstance.GetModelInstance(target);

			// Set the properties to an empty array if null
			if (properties == null)
				properties = new string[0];

			// Create a single condition target if the specified properties are all on the root
			if (!properties.Any(property => property.Contains('.') || property.Contains('{')))
				targets.Add(new ConditionTarget(this, root, properties));

			// Otherwise, process the property paths to create the necessary sources
			else
			{			
				// Process each property path to build up the condition sources
				foreach (string property in properties)
					AddTarget(root, root.Type.GetPath(property).FirstSteps);
			}
		}

		/// <summary>
		/// Recursively attaches the current condition to targets in the model.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="steps"></param>
		void AddTarget(ModelInstance target, IEnumerable<ModelStep> steps)
		{
			// Get or create a condition target for the current instance
			ConditionTarget conditionTarget = targets.FirstOrDefault(ct => ct.Target == target);
			if (conditionTarget == null)
			{
				conditionTarget = new ConditionTarget(this, target);
				targets.Add(conditionTarget);
			}

			// Process each step along the path
			foreach (var step in steps)
			{
				// Add the property for the current step
				conditionTarget.AddProperty(step.Property.Name);

				// Stop processing if the current step is a leaf property
				if (step.NextSteps.Count == 0)
					continue;

				// Recursively process child instances
				foreach (var child in step.GetInstances(target))
					AddTarget(child, step.NextSteps);
			}
		}

		/// <summary>
		/// Gets the set of <see cref="Condition"/> instances associated with the specified <see cref="ModelInstance"/>.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public static IEnumerable<Condition> GetConditions(ModelInstance instance)
		{
			return instance.GetExtension<RuleManager>().GetConditions();
		}

		/// <summary>
		/// Gets the set of <see cref="Condition"/> instances associated with the specified <see cref="ModelInstance"/>.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="filter"></param>
		/// <returns></returns>
		public static IEnumerable<Condition> GetConditions(ModelInstance instance, Func<ConditionTarget, bool> filter)
		{
			return instance.GetExtension<RuleManager>().GetConditions(filter);
		}

		/// <summary>
		/// Gets the set of <see cref="Condition"/> instances associated with the specified <see cref="ModelInstance"/>.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public static IEnumerable<Condition> GetConditions(object instance)
		{
			ModelInstance modelInstance = ModelInstance.GetModelInstance(instance);

			if (modelInstance == null)
				throw new ArgumentException("Specified instance is not a valid ModelInstance");

			return modelInstance.GetExtension<RuleManager>().GetConditions();
		}

		/// <summary>
		/// Destroys the current exception.
		/// </summary>
		internal void Destroy()
		{
			foreach (ConditionTarget conditionTarget in targets)
				conditionTarget.Target.GetExtension<RuleManager>().ClearCondition(conditionTarget.Condition.Type);
			targets.Clear();
		}


		/// <summary>
		/// Allows conditions to be thrown as if they were exceptions.
		/// </summary>
		/// <param name="condition"></param>
		/// <returns></returns>
		public static implicit operator Exception(Condition condition)
		{
			return new ApplicationException(condition.Message);
		}

		#endregion
	}

	#endregion
}
