// Copyright 2004-2010 Castle Project - http://www.castleproject.org/
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

namespace Castle.DynamicProxy.Generators
{
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Reflection.Emit;
	using Castle.DynamicProxy.Generators.Emitters;

	public class MetaProperty : MetaTypeElement, IEquatable<MetaProperty>
	{
		private string name;
		private readonly Type type;
		private readonly MetaMethod getter;
		private readonly MetaMethod setter;
		private readonly PropertyAttributes attributes;
		private readonly IEnumerable<CustomAttributeBuilder> customAttributes;
		private PropertyEmitter emitter;

		public MetaProperty(string name, Type propertyType, Type declaringType, MetaMethod getter, MetaMethod setter, PropertyAttributes attributes, IEnumerable<CustomAttributeBuilder> customAttributes)
			:base(declaringType)
		{
			this.name = name;
			this.type = propertyType;
			this.getter = getter;
			this.setter = setter;
			this.attributes = attributes;
			this.customAttributes = customAttributes;
		}

		public bool CanRead
		{
			get { return getter != null; }
		}

		public bool CanWrite
		{
			get { return setter != null; }
		}

		public MethodInfo GetMethod
		{
			get
			{
				if(!CanRead)
				{
					throw new InvalidOperationException();
				}
				return getter.Method;
			}
		}

		public MethodInfo SetMethod
		{
			get
			{
				if(!CanWrite)
				{
					throw new InvalidOperationException();
				}
				return setter.Method;
			}
		}

		public PropertyEmitter Emitter
		{
			get {
				if (emitter == null)
					throw new InvalidOperationException(
						"Emitter is not initialized. You have to initialize it first using 'BuildPropertyEmitter' method");
				return emitter;
			}
		}

		public MetaMethod Getter
		{
			get { return getter; }
		}

		public MetaMethod Setter
		{
			get { return setter; }
		}

		public bool Equals(MetaProperty other)
		{
			if (ReferenceEquals(null, other))
			{
				return false;
			}

			if (ReferenceEquals(this, other))
			{
				return true;
			}

			if (!type.Equals(other.type))
			{
				return false;
			}

			if(!StringComparer.OrdinalIgnoreCase.Equals(name,other.name))
			{
				return false;
			}

			return true;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
			{
				return false;
			}
			if (ReferenceEquals(this, obj))
			{
				return true;
			}
			if (obj.GetType() != typeof(MetaProperty))
			{
				return false;
			}
			return Equals((MetaProperty) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((GetMethod != null ? GetMethod.GetHashCode() : 0) * 397) ^ (SetMethod != null ? SetMethod.GetHashCode() : 0);
			}
		}

		public void BuildPropertyEmitter(ClassEmitter classEmitter)
		{
			if (emitter != null)
				throw new InvalidOperationException("Emitter is already created. It is illegal to invoke this method twice.");

			emitter = classEmitter.CreateProperty(name, attributes, type);
			foreach (var attribute in customAttributes)
			{
				emitter.DefineCustomAttribute(attribute);
			}
		}

		internal override void SwitchToExplicitImplementation()
		{
			name = string.Format("{0}.{1}", sourceType.Name, name);
			if(setter!=null)
			{
				setter.SwitchToExplicitImplementation();
			}
			if(getter!=null)
			{
				getter.SwitchToExplicitImplementation();
			}
		}
	}
}