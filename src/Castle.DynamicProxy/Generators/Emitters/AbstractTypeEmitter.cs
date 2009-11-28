// Copyright 2004-2009 Castle Project - http://www.castleproject.org/
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Castle.DynamicProxy.Generators.Emitters
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Reflection;
	using System.Reflection.Emit;
	using Castle.DynamicProxy.Generators.Emitters.SimpleAST;

	public abstract class AbstractTypeEmitter
	{
		private const MethodAttributes defaultAttributes = MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public;
		private readonly TypeBuilder typebuilder;
		private readonly ConstructorCollection constructors;
		private readonly MethodCollection methods;
		private readonly PropertiesCollection properties;
		private readonly EventCollection events;
		private readonly NestedClassCollection nested;
		private readonly Dictionary<String, GenericTypeParameterBuilder> name2GenericType;

		private GenericTypeParameterBuilder[] genericTypeParams;

		private readonly IDictionary<string, FieldReference> fields =
			new Dictionary<string, FieldReference>(StringComparer.OrdinalIgnoreCase);

		public AbstractTypeEmitter(TypeBuilder typeBuilder)
		{
			typebuilder = typeBuilder;
			nested = new NestedClassCollection();
			methods = new MethodCollection();
			constructors = new ConstructorCollection();
			properties = new PropertiesCollection();
			events = new EventCollection();
			name2GenericType = new Dictionary<String, GenericTypeParameterBuilder>();
		}

		public Type GetGenericArgument(String genericArgumentName)
		{
			return name2GenericType[genericArgumentName];
		}

		public Type[] GetGenericArgumentsFor(Type genericType)
		{
			List<Type> types = new List<Type>();

			foreach (Type genType in genericType.GetGenericArguments())
			{
				if (genType.IsGenericParameter)
				{
					types.Add(name2GenericType[genType.Name]);
				}
				else
				{
					types.Add(genType);
				}
			}

			return types.ToArray();
		}

		public Type[] GetGenericArgumentsFor(MethodInfo genericMethod)
		{
			List<Type> types = new List<Type>();
			foreach (Type genType in genericMethod.GetGenericArguments())
			{
				types.Add(name2GenericType[genType.Name]);
			}

			return types.ToArray();
		}

		public void AddCustomAttributes(ProxyGenerationOptions proxyGenerationOptions)
		{
			foreach (Attribute attr in proxyGenerationOptions.attributesToAddToGeneratedTypes)
			{
				var customAttributeBuilder = AttributeUtil.CreateBuilder(attr);
				if (customAttributeBuilder != null)
				{
					typebuilder.SetCustomAttribute(customAttributeBuilder);
				}
			}

			foreach (var attribute in proxyGenerationOptions.AdditionalAttributes)
			{
				typebuilder.SetCustomAttribute(attribute);
			}
		}

		public void CreateDefaultConstructor()
		{
			if (TypeBuilder.IsInterface)
				throw new InvalidOperationException ("Interfaces cannot have constructors.");

			constructors.Add(new ConstructorEmitter(this));
		}

		public ConstructorEmitter CreateConstructor(params ArgumentReference[] arguments)
		{
			if (TypeBuilder.IsInterface)
				throw new InvalidOperationException ("Interfaces cannot have constructors.");

			ConstructorEmitter member = new ConstructorEmitter(this, arguments);
			constructors.Add(member);
			return member;
		}

		public ConstructorEmitter CreateTypeConstructor()
		{
			var member = new TypeConstructorEmitter(this);
			constructors.Add(member);
			ClassConstructor = member;
			return member;
		}

		public TypeConstructorEmitter ClassConstructor { get; private set; }

		public MethodEmitter CreateMethod(String name, MethodAttributes attributes)
		{
			MethodEmitter member = new MethodEmitter(this, name, attributes);
			methods.Add(member);
			return member;
		}

		public MethodEmitter CreateMethod(string name, MethodAttributes attrs, Type returnType)
		{
			return CreateMethod(name, attrs, returnType, Type.EmptyTypes);
		}

		public MethodEmitter CreateMethod(string name, MethodAttributes attrs, Type returnType,
		                                  params Type[] argumentTypes)
		{
			var member = new MethodEmitter(this, name, attrs, returnType, argumentTypes);
			methods.Add(member);
			return member;
		}

		public MethodEmitter CreateMethod(string name, Type returnType, params ArgumentReference[] argumentReferences)
		{
			return CreateMethod(name, defaultAttributes, returnType, argumentReferences);
		}

		public MethodEmitter CreateMethod(string name, MethodAttributes attrs, Type returnType,
		                                  params ArgumentReference[] argumentReferences)
		{
			Type[] argumentTypes = ArgumentsUtil.InitializeAndConvert(argumentReferences);
			return CreateMethod(name, attrs, returnType, argumentTypes);
		}

		public MethodEmitter CreateMethod(string name, params ArgumentReference[] arguments)
		{
			return CreateMethod(name, typeof(void), arguments);
		}

		public FieldReference CreateStaticField(string name, Type fieldType)
		{
			FieldAttributes atts = FieldAttributes.Public;

			return CreateStaticField(name, fieldType, atts);
		}

		public FieldReference CreateStaticField(string name, Type fieldType, FieldAttributes atts)
		{
			atts |= FieldAttributes.Static;
			return CreateField(name, fieldType, atts);
		}

		public FieldReference CreateField(string name, Type fieldType)
		{
			return CreateField(name, fieldType, true);
		}

		public FieldReference CreateField(string name, Type fieldType, bool serializable)
		{
			FieldAttributes atts = FieldAttributes.Public;

			if (!serializable)
			{
				atts |= FieldAttributes.NotSerialized;
			}

			return CreateField(name, fieldType, atts);
		}

		public FieldReference CreateField(string name, Type fieldType, FieldAttributes atts)
		{
			FieldBuilder fieldBuilder = typebuilder.DefineField(name, fieldType, atts);
			var reference = new FieldReference(fieldBuilder);
			fields[name] = reference;
			return reference;
		}

		public PropertyEmitter CreateProperty(String name, PropertyAttributes attributes, Type propertyType)
		{
			PropertyEmitter propEmitter = new PropertyEmitter(this, name, attributes, propertyType);
			properties.Add(propEmitter);
			return propEmitter;
		}


		public EventEmitter CreateEvent(string name, EventAttributes atts, Type type)
		{
			EventEmitter eventEmitter = new EventEmitter(this, name, atts, type);
			events.Add(eventEmitter);
			return eventEmitter;
		}

		
		public void DefineCustomAttribute(CustomAttributeBuilder attribute)
		{
			typebuilder.SetCustomAttribute(attribute);
		}

		public void DefineCustomAttribute<TAttribute>(object[] constructorArguments) where TAttribute:Attribute
		{
			var customAttributeBuilder = AttributeUtil.CreateBuilder(typeof (TAttribute), constructorArguments);
			typebuilder.SetCustomAttribute(customAttributeBuilder);
		}

		public void DefineCustomAttribute<TAttribute>() where TAttribute : Attribute, new()
		{
			var customAttributeBuilder = AttributeUtil.CreateBuilder<TAttribute>();
			typebuilder.SetCustomAttribute(customAttributeBuilder);
		}

		public void DefineCustomAttributeFor<TAttribute>(FieldReference field) where TAttribute : Attribute, new()
		{
			var customAttributeBuilder = AttributeUtil.CreateBuilder<TAttribute>();
			var fieldbuilder = field.Fieldbuilder;
			if(fieldbuilder==null)
			{
				throw new ArgumentException("Invalid field reference.This reference does not point to field on type being generated","field");
			}
			fieldbuilder.SetCustomAttribute(customAttributeBuilder);
		}


		public ConstructorCollection Constructors
		{
			get { return constructors; }
		}

		public NestedClassCollection Nested
		{
			get { return nested; }
		}

		public TypeBuilder TypeBuilder
		{
			get { return typebuilder; }
		}

		public Type BaseType
		{
			get 
			{
				if (TypeBuilder.IsInterface)
					throw new InvalidOperationException ("This emitter represents an interface; interfaces have no base types.");
				return TypeBuilder.BaseType; 
			}
		}

		public GenericTypeParameterBuilder[] GenericTypeParams
		{
			get { return genericTypeParams; }
		}

		public void SetGenericTypeParameters(GenericTypeParameterBuilder[] genericTypeParameterBuilders)
		{
			this.genericTypeParams = genericTypeParameterBuilders;
		}

		public void CopyGenericParametersFromMethod (MethodInfo methodToCopyGenericsFrom)
		{
			// big sanity check
			if (genericTypeParams != null)
			{
				throw new ProxyGenerationException("CopyGenericParametersFromMethod: cannot invoke me twice");
			}

			SetGenericTypeParameters(GenericUtil.CopyGenericArguments(methodToCopyGenericsFrom, typebuilder, name2GenericType));
		}

		public FieldReference GetField(string name)
		{
			if(string.IsNullOrEmpty(name))
				return null;

			FieldReference value;
			fields.TryGetValue(name, out value);
			return value;
		}

		public IEnumerable<FieldReference> GetAllFields()
		{
			return fields.Values;
		}

		public virtual Type BuildType()
		{
			EnsureBuildersAreInAValidState();

			Type type = CreateType(typebuilder);

			foreach (NestedClassEmitter builder in nested)
			{
				builder.BuildType();
			}

			return type;
		}

		protected virtual void EnsureBuildersAreInAValidState()
		{
			if (!typebuilder.IsInterface && constructors.Count == 0)
			{
				CreateDefaultConstructor();
			}

			foreach (IMemberEmitter builder in properties)
			{
				builder.EnsureValidCodeBlock();
				builder.Generate();
			}
			foreach (IMemberEmitter builder in events)
			{
				builder.EnsureValidCodeBlock();
				builder.Generate();
			}
			foreach (IMemberEmitter builder in constructors)
			{
				builder.EnsureValidCodeBlock();
				builder.Generate();
			}
			foreach (IMemberEmitter builder in methods)
			{
				builder.EnsureValidCodeBlock();
				builder.Generate();
			}
		}

		protected Type CreateType(TypeBuilder type)
		{
			try
			{
				return type.CreateType();
			}
			catch (BadImageFormatException ex)
			{
				if (Debugger.IsAttached == false)
				{
					throw;
				}

				if (ex.Message.Contains(@"HRESULT: 0x8007000B") == false)
				{
					throw;
				}

				if (type.IsGenericTypeDefinition == false)
				{
					throw;
				}

				var message =
					"This is a DynamicProxy2 error: It looks like you enoutered a bug in Visual Studio debugger, " +
					"which causes this exception when proxying types with generic methods having constraints on their generic arguments." +
					"This code will work just fine without the debugger attached. " +
					"If you wish to use debugger you may have to switch to Visual Studio 2010 where this bug was fixed.";
				var exception = new ProxyGenerationException(message);
				exception.Data.Add("ProxyType", type.ToString());
				throw exception;
			}
		}
	}
}
