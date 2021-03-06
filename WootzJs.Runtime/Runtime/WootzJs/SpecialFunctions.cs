#region License
//-----------------------------------------------------------------------
// <copyright>
// The MIT License (MIT)
// 
// Copyright (c) 2014 Kirk S Woll
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------
#endregion

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace System.Runtime.WootzJs
{
    /// <summary>
    /// This is a special class handled by the compiler such that each method is declared as
    /// a global function.
    /// </summary>
    public static class SpecialFunctions
    {
        [Js(Name = "$define")]
        public static JsTypeFunction Define(JsFunction assembly, JsTypeFunction enclosingType, string name, bool isGenericType, JsArray typeParameters, JsObject prototype, JsFunction typeInitializer)
        {
            JsTypeFunction typeFunction = null;
            var isTypeInitialized = false;

            // Create constructor function, which is a superconstructor that takes in the actual
            // constructor as the first argument, and the rest of the arguments are passed directly 
            // to that constructor.  These subconstructors are not Javascript constructors -- they 
            // are not called via new, they exist for initialization only.
            typeFunction = Jsni.function((constructor, args) =>
            {
                if (constructor != null || !(Jsni.instanceof(Jsni.@this(), typeFunction)))
                {
                    if (!isGenericType || typeFunction.UnconstructedType != null)
                        typeFunction.member(SpecialNames.StaticInitializer).invoke();
                }
                if (constructor != null) 
                    constructor.apply(Jsni.@this(), args.As<JsArray>());
                if (!Jsni.instanceof(Jsni.@this(), typeFunction))
                    return typeFunction;
                else
                    return Jsni.@this();
            }).As<JsTypeFunction>();
            typeFunction.GetAssembly = assembly;
            assembly.member(SpecialNames.AssemblyTypesArray).member("push").invoke(typeFunction);
            typeFunction.memberset("toString", Jsni.function(() => name.As<JsObject>()));
            typeFunction.EnclosingType = enclosingType;
            typeFunction.TypeName = name;
            typeFunction.prototype = Jsni.@new(prototype);
            typeFunction.IsPrototypeInitialized = false;
            typeFunction.TypeInitializer = Jsni.procedure((_t, p) =>
            {
                var t = _t.As<JsTypeFunction>();
                if (isGenericType)
                {
                    var unconstructedType = t.UnconstructedType ?? t;
                    t.GenericTypeFunction = Jsni.function(() =>
                    {
                        return Jsni.reference(SpecialNames.MakeGenericTypeConstructor).As<JsFunction>().call(unconstructedType, unconstructedType, Jsni.arguments()).As<JsFunction>().invoke();
                    });                
                }
                t.GetTypeFromType = Jsni.function(() =>
                {
                    return Type._GetTypeFromTypeFunc(Jsni.@this().As<JsTypeFunction>()).As<JsObject>();
                });
                p.memberset(SpecialNames.TypeName, t.member(SpecialNames.TypeName));
                p.___type = t;
                t.BaseType = prototype.As<JsTypeFunction>();

                typeInitializer.apply(Jsni.@this(), Jsni.arguments().As<JsArray>());
            });
            typeFunction.CallTypeInitializer = Jsni.procedure(() =>
            {
                typeFunction.TypeInitializer.apply(enclosingType, Jsni.array(typeFunction, typeFunction.prototype).concat(typeParameters));
            });
            return typeFunction;
        }

        [Js(Name = SpecialNames.NullPropagation)]
        public static JsObject NullPropagation(JsObject @this, JsObject target, JsObject ifNull, JsFunction ifNotNull)
        {
            if (target == null)
                return ifNull;
            else
                return ifNotNull.call(@this, target);
        }

        [Js(Name = SpecialNames.DefineStaticConstructor)]
        public static JsFunction DefineStaticConstructor(JsTypeFunction enclosingType, JsFunction implementation)
        {
            return Jsni.procedure(() =>
            {
                if (enclosingType.IsStaticInitialized)
                {
                    return;
                }          
                enclosingType.IsStaticInitialized = true;
                implementation.invoke();
            });
        }

        [Js(Name = SpecialNames.DefineConstructor)]
        public static JsFunction CreateConstructor(JsTypeFunction enclosingType, JsFunction implementation)
        {
            implementation.memberset(SpecialNames.TypeField, enclosingType);
            implementation.memberset(SpecialNames.New, Jsni.function(() =>
            {
/*
                if (!enclosingType.IsPrototypeInitialized)
                {
                    enclosingType.IsPrototypeInitialized = true;
                    enclosingType.prototype = Jsni.@new(enclosingType.PrototypeFactory.invoke());
                }
*/
                return Jsni.@new(enclosingType, implementation, Jsni.arguments());
            }));

            return implementation;
        }

        [Js(Name = SpecialNames.DefineTypeFunction)]
        public static JsFunction DefineTypeFunction(JsTypeFunction type, JsFunction template)
        {
            return template;
        }

        [Js(Name = SpecialNames.DefineTypeParameter)]
        public static JsTypeFunction DefineTypeParameter(JsFunction assembly, string name, JsTypeFunction prototype)
        {
            var result = Define(assembly, null, name, false, Jsni.array(), prototype, Jsni.procedure(() => {}));
            result.memberset(SpecialNames.IsTypeParameter, true);
            result.memberset(SpecialNames.CreateType, Jsni.function(() =>
            {
                var type = Type.CreateTypeParameter(name, prototype);
                result.Type = type;
                type.thisType = result;
                return result;
            }));
            return result;
        }

        [Js(Name = "$cast")]
        public static T ObjectCast<T>(object o)
        {
            if (o == null && typeof(T).IsValueType)
                throw new InvalidCastException("Cannot convert null to '" + typeof(T).FullName + "' because it is a non-nullable value type");
            if (o == null)
                return o.As<T>();
            var type = o.GetType();
            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var typeArgument = typeof(T).GetGenericTypeArgument(0);
                if (typeArgument.IsAssignableFrom(type))
                    return o.As<T>();
                if (typeArgument == typeof(char) && type == typeof(string))
                    return o.As<T>();
            }
            if (typeof(T) == typeof(char) && type == typeof(string))
                return o.As<T>();

            if (!typeof(T).IsAssignableFrom(type))
                throw new InvalidCastException("Cannot cast object of type " + o.GetType().FullName + " to type " + typeof(T).FullName);

            return o.As<T>();
        }

        [Js(Name = "$delegate")]
        public static JsFunction CreateDelegate(JsObject thisExpression, JsFunction lambda, JsTypeFunction delegateType = null, string delegateKey = null)
        {
            delegateType = delegateType ?? Jsni.reference("System.Delegate").As<JsTypeFunction>();

            JsFunction delegateFunc = null;
            delegateFunc = Jsni.function(() =>
            {
                return lambda.apply(delegateFunc.As<Delegate>().Target.As<JsObject>(), Jsni.arguments().As<JsArray>());
            });
            delegateFunc.prototype = Jsni.@new(delegateType);
            delegateFunc.memberset("get_Target", Jsni.function(() => thisExpression));
            delegateFunc.memberset("GetType", Jsni.function(() => delegateType.GetTypeFromType.invoke()));
            Jsni.memberset(delegateFunc, SpecialNames.TypeField, delegateType);
            Jsni.memberset(delegateFunc, "Invoke", delegateFunc);
            Jsni.memberset(delegateFunc, "DynamicInvoke", Jsni.function(args => delegateFunc.apply(delegateFunc, args.As<JsArray>())));
            Jsni.memberset(delegateFunc, "GetHashCode", Jsni.function(() => lambda.toString().GetHashCode().As<JsObject>()));
            Jsni.memberset(delegateFunc, "lambda", lambda);
            Jsni.memberset(delegateFunc, "Equals", Jsni.function(x => x != null && lambda == x.member("lambda")));
            return delegateFunc;
        }

        [Js(Name=SpecialNames.InitializeArray)]
        internal static JsArray InitializeArray(JsArray array, JsTypeFunction elementType)
        {
            if (array.member("$isInitialized"))
                return array;

            array.memberset("$isInitialized", true);
            var arrayType = MakeArrayType(elementType);

            // This is way faster than the foreach/for...in that was used below.  So we'll just copy the methods we need manually.
            array["System$Collections$Generic$IEnumerable$1$GetEnumerator"] = arrayType.member("prototype")["System$Collections$Generic$IEnumerable$1$GetEnumerator"];
            array["GetEnumerator"] = arrayType.member("prototype")["System$Collections$Generic$IEnumerable$1$GetEnumerator"];
            array["GetType"] = arrayType.member("prototype")["GetType"];
            array["$type"] = arrayType.member("prototype")["$type"];
            array["System$Collections$Generic$IReadOnlyList$1$get_Item"] = arrayType.member("prototype")["System$Collections$Generic$IReadOnlyList$1$get_Item"];
/*
            if (elementType.TypeName == "System.Int32")
            {
                array["System$Collections$IEnumerable$GetEnumerator"] = arrayType.member("prototype")["System$Collections$IEnumerable$GetEnumerator"];
                array["System$Collections$ICollection$get_Count"] = arrayType.member("prototype")["System$Collections$ICollection$get_Count"];
                array["System$Collections$Generic$IReadOnlyCollection$1$get_Count"] = arrayType.member("prototype")["System$Collections$Generic$IReadOnlyCollection$1$get_Count"];
                array["System$Collections$ICollection$get_SyncRoot"] = arrayType.member("prototype")["System$Collections$ICollection$get_SyncRoot"];
                array["System$Collections$ICollection$get_IsSynchronized"] = arrayType.member("prototype")["System$Collections$ICollection$get_IsSynchronized"];
                array["System$Collections$ICollection$CopyTo"] = arrayType.member("prototype")["System$Collections$ICollection$CopyTo"];
                array["System$Collections$IList$get_IsReadOnly"] = arrayType.member("prototype")["System$Collections$IList$get_IsReadOnly"];
                array["System$Collections$IList$get_IsFixedSize"] = arrayType.member("prototype")["System$Collections$IList$get_IsFixedSize"];
                array["System$Collections$IList$Add"] = arrayType.member("prototype")["System$Collections$IList$Add"];
                array["System$Collections$IList$Contains"] = arrayType.member("prototype")["System$Collections$IList$Contains"];
                array["System$Collections$IList$Clear"] = arrayType.member("prototype")["System$Collections$IList$Clear"];
                array["System$Collections$IList$Insert"] = arrayType.member("prototype")["System$Collections$IList$Insert"];
                array["System$Collections$IList$Remove"] = arrayType.member("prototype")["System$Collections$IList$Remove"];
                array["System$Collections$IList$RemoveAt"] = arrayType.member("prototype")["System$Collections$IList$RemoveAt"];
                array["GetValue"] = arrayType.member("prototype")["GetValue"];
                array["GetEnumerator"] = arrayType.member("prototype")["GetEnumerator"];
                array["get_Count"] = arrayType.member("prototype")["get_Count"];
                array["get_SyncRoot"] = arrayType.member("prototype")["get_SyncRoot"];
                array["get_IsSynchronized"] = arrayType.member("prototype")["get_IsSynchronized"];
                array["Copy"] = arrayType.member("prototype")["Copy"];
                array["Copy$2"] = arrayType.member("prototype")["Copy$2"];
                array["ConstrainedCopy"] = arrayType.member("prototype")["ConstrainedCopy"];
                array["Copy$1"] = arrayType.member("prototype")["Copy$1"];
                array["Copy$3"] = arrayType.member("prototype")["Copy$3"];
            }
*/

//            foreach (var property in arrayType.member("prototype"))
//            {
//                array[property] = arrayType.member("prototype")[property];
//            }

            arrayType.member("prototype").member("$ctor").member("call").invoke(array);

            return array;
        }

        [Js(Name = SpecialNames.MakeGenericTypeConstructor)]
        internal static JsTypeFunction MakeGenericTypeFactory(JsTypeFunction unconstructedType, JsArray typeArgs)
        {
            var cache = Jsni.member(unconstructedType, SpecialNames.TypeCache);
            JsObject result = null;
            if (typeArgs.length > 0)
            {
                if (cache == null)
                {
                    cache = new JsObject();
                    unconstructedType.memberset(SpecialNames.TypeCache, cache);
                }                

                JsObject currentCache = cache;
                for (var i = 0; i < typeArgs.length - 1; i++)
                {
                    var nextCache = currentCache[typeArgs[i].member(SpecialNames.TypeName)];
                    if (nextCache == null)
                    {
                        nextCache = new JsObject();
                        currentCache[typeArgs[i].member(SpecialNames.TypeName)] = nextCache;
                    }
                    currentCache = nextCache;
                }
                cache = currentCache;
                result = cache[typeArgs[typeArgs.length - 1].member(SpecialNames.TypeName)];
            }
            else
            {
                result = cache;
            }

//            JsObject keyString;

            // First two if statements are optimizations for performance reasons to avoid heap allocation when possible.
/*
            if (typeArgs.length == 0)
                keyString = "";
            else if (typeArgs.length == 1)
                keyString = typeArgs[0].member(SpecialNames.TypeName);
            else
            {
                var keyArray = Jsni.call<JsArray>(x => x.slice(0), typeArgs, 0.As<JsNumber>()).As<JsArray>();
                var keyParts = new JsArray();
                for (var i = 0; i < keyArray.length; i++)
                {
                    keyParts[i] = keyArray[i].member(SpecialNames.TypeName);
                }
                keyString = keyParts.join(", ");                
            }

            var result = cache[keyString];
*/
            if (result == null)
            {
                var keyArray = Jsni.call<JsArray>(x => x.slice(0), typeArgs, 0.As<JsNumber>()).As<JsArray>();
                var keyParts = new JsArray();
                for (var i = 0; i < keyArray.length; i++)
                {
                    keyParts[i] = keyArray[i].member(SpecialNames.TypeName);
                }
                var keyString = keyParts.join(", ");                

                var lastIndexOfDollar = unconstructedType.TypeName.LastIndexOf('`');
                if (lastIndexOfDollar == -1)
                    lastIndexOfDollar = unconstructedType.TypeName.Length;
                var newTypeName = unconstructedType.TypeName.Substring(0, lastIndexOfDollar) + "<" + keyString + ">";
                var prototype = unconstructedType.BaseType;
                if (prototype.member("$"))
                {
                    var baseArgs = unconstructedType.BaseTypeArgs.slice(0);
                    for (var i = 0; i < baseArgs.length; i++)
                    {
                        var baseArg = (JsTypeFunction)baseArgs[i];
                        if (baseArg.IsTypeParameter)
                        {
                            for (var j = 0; j < unconstructedType.TypeArgs.length; j++)
                            {
                                var typeArg = unconstructedType.TypeArgs[j];
                                if (typeArg == baseArg)
                                {
                                    baseArgs[i] = typeArgs[j];
                                }
                            }
                        }
                    }
                    prototype = prototype.member("$").apply(null, baseArgs).As<JsTypeFunction>();
                }
                var typeInitializer = Jsni.procedure((_t, p) =>
                {
                    var t = _t.As<JsTypeFunction>();
                    p.___type = t;
                    t.As<JsTypeFunction>().BaseType = unconstructedType;
                    t.As<JsTypeFunction>().GetTypeFromType = Jsni.function(() => Type._GetTypeFromTypeFunc(Jsni.@this().As<JsTypeFunction>()).As<JsObject>());
                    t.As<JsTypeFunction>().TypeName = newTypeName;
                    t.As<JsTypeFunction>().CreateTypeField = Jsni.function(() =>
                    {
                        var unconstructedTypeType = Type._GetTypeFromTypeFunc(unconstructedType);
                        var type = new Type(newTypeName, new Attribute[0]);
                        t.Type = type;
                        type.thisType = t;

                        var typeParameters = unconstructedTypeType.typeArguments;
                        var typeArguments = InitializeArray(typeArgs, Jsni.type<JsTypeFunction>()).As<JsTypeFunction[]>();

                        Func<Type, JsTypeFunction> reifyGenerics = null;
                        reifyGenerics = theType =>
                        {
                            if (theType == null)
                            {
                                return null;
                            }
                            if (theType.IsArray)
                            {
                                var elementType = theType.GetElementType();
                                var arrayType = MakeArrayType(reifyGenerics(elementType));
                                return arrayType;
                            }
                            if (theType.IsGenericParameter)
                            {
                                for (int i = 0; i < typeArguments.Length; i++)
                                {
                                    var typeParameter = typeParameters[i];
                                    var typeArgument = typeArguments[i];
                                    if (typeParameter.TypeName == theType.FullName)
                                    {
                                        return typeArgument;
                                    }
                                }
                            }
                            else if (theType.IsGenericTypeDefinition)
                            {
                                return MakeGenericTypeFactory(theType.thisType, theType.GenericTypeArguments.Select(x => reifyGenerics(x)).ToArray().As<JsArray>());
                            }
                            else if (theType.IsGenericType)
                            {
                                JsTypeFunction[] newTypeArguments = null;
                                for (var i = 0; i < theType.typeArguments.Length; i++)
                                {
                                    var theTypeArgument = theType.typeArguments[i];
                                    for (var j = 0; j < typeParameters.Length; j++)
                                    {
                                        var typeParameter = typeParameters[j];
                                        var typeArgument = typeArguments[j];
                                        if (theTypeArgument == typeParameter)
                                        {
                                            if (newTypeArguments == null)
                                            {
                                                newTypeArguments = new JsTypeFunction[theType.typeArguments.Length];
                                                for (var k = 0; k < theType.typeArguments.Length; k++)
                                                {
                                                    newTypeArguments[k] = theType.typeArguments[k];
                                                }
                                            }
                                            newTypeArguments[i] = typeArgument;
                                        }
                                    }
                                }
                                if (newTypeArguments != null)
                                    return MakeGenericTypeFactory(theType.unconstructedType, newTypeArguments.As<JsArray>());
                                else
                                    return theType.thisType;
                            }
                            return theType.thisType;
                        };

                        var newInterfaces = unconstructedTypeType.interfaces.ToArray();
                        for (var i = 0; i < newInterfaces.Length; i++)
                        {
                            var intf = newInterfaces[i];
                            intf = reifyGenerics(Type._GetTypeFromTypeFunc(intf));
                            newInterfaces[i] = intf;
                        }

                        var newConstructors = unconstructedTypeType.constructors.ToArray();
                        for (var i = 0; i < newConstructors.Length; i++)
                        {
                            var constructor = newConstructors[i];
                            var parameters = constructor.GetParameters();
                            for (var j = 0; j < parameters.Length; j++)
                            {
                                var parameter = parameters[j];
                                var parameterType = reifyGenerics(parameter.ParameterType);
                                parameter = new ParameterInfo(parameter.Name, parameterType, j, parameter.Attributes, parameter.DefaultValue, parameter.attributes);
                                parameters[j] = parameter;
                            }
                            newConstructors[i] = new ConstructorInfo(constructor.Name, t.prototype[constructor.Name].As<JsFunction>(), parameters, constructor.Attributes, constructor.attributes);
                        }

                        var newProperties = unconstructedTypeType.properties.ToArray();
                        for (var i = 0; i < newProperties.Length; i++)
                        {
                            var property = newProperties[i];
                            var propertyTypeType = property.PropertyType;
                            var propertyType = reifyGenerics(propertyTypeType);
                            newProperties[i] = new PropertyInfo(property.Name, propertyType, property.GetGetMethod(), property.GetSetMethod(), 
                                property.GetIndexParameters(), property.attributes);
                        }

                        var newMethods = unconstructedTypeType.methods.ToArray();
                        for (var i = 0; i < newMethods.Length; i++)
                        {
                            var method = newMethods[i];
                            var returnTypeType = method.ReturnType;
                            var returnType = reifyGenerics(returnTypeType);
                            var parameters = method.GetParameters();
                            for (var j = 0; j < parameters.Length; j++)
                            {
                                var parameter = parameters[j];
                                var parameterType = reifyGenerics(parameter.ParameterType);
                                parameter = new ParameterInfo(parameter.Name, parameterType, j, parameter.Attributes, parameter.DefaultValue, parameter.attributes);
                                parameters[j] = parameter;
                            }
                            method = new MethodInfo(method.Name, method.jsMethod, parameters, returnType, method.Attributes, method.attributes);
                            newMethods[i] = method;
                        }

                        type.Init(
                            newTypeName, 
                            (int)((unconstructedTypeType.typeFlags | TypeFlags.GenericType) & ~TypeFlags.GenericTypeDefenition),
                            t, 
                            prototype,
                            newInterfaces, 
                            typeArguments,
                            unconstructedTypeType.fields, 
                            newMethods, 
                            newConstructors, 
                            newProperties, 
                            unconstructedTypeType.events, 
                            null, 
                            unconstructedType);
                        return type.As<JsObject>();
                    });

                }, SpecialNames.TypeInitializerTypeFunction, SpecialNames.TypeInitializerPrototype);

                var generic = Define(unconstructedType.GetAssembly, unconstructedType.EnclosingType, newTypeName, true, Jsni.array(), prototype, typeInitializer);
                generic.memberset(SpecialNames.UnconstructedType, unconstructedType);

                // unconstructedType.$TypeInitializer.apply(this, [generic, generic.prototype].concat(Array.prototype.slice.call(arguments, 0)));
                unconstructedType.member(SpecialNames.TypeInitializer).apply(
                    unconstructedType, 
                    Jsni.array(generic, generic.prototype).member("concat").invoke(keyArray).As<JsArray>()
                );

                Jsni.call(generic.TypeInitializer, Jsni.@this(), generic, generic.prototype);
                result = generic;

                if (typeArgs.length == 0)
                    unconstructedType.memberset(SpecialNames.TypeCache, result);
                else
                    cache[typeArgs[typeArgs.length - 1].member(SpecialNames.TypeName)] = result;
            }

            return result.As<JsTypeFunction>();
        }

        [Js(Name = SpecialNames.MakeArrayType)]
        internal static JsTypeFunction MakeArrayType(JsTypeFunction elementType)
        {
            if (elementType.ArrayType == null)
            {
                var baseType = MakeGenericTypeFactory(Jsni.type(typeof(GenericArray<>)), Jsni.array(elementType));
                var arrayType = Jsni.procedure(() => {}).As<JsTypeFunction>();
                arrayType.prototype = Jsni.@new(baseType);
                Jsni.apply(
                    Jsni.type<Object>().TypeInitializer, 
                    Jsni.@this(), 
                    Jsni.array(arrayType, arrayType.prototype));
                Jsni.apply(
                    Jsni.type<Array>().TypeInitializer, 
                    Jsni.@this(), 
                    Jsni.invoke(
                        Jsni.member(Jsni.array(arrayType, arrayType.prototype), "concat"), 
                        elementType
                    ).As<JsArray>());
                arrayType.TypeInitializer = Jsni.procedure((t, p) =>
                {
                    p.___type = arrayType;
                    t.As<JsTypeFunction>().TypeName = elementType.TypeName + "[]";
                    t.As<JsTypeFunction>().BaseType = baseType;
                    t.As<JsTypeFunction>().ElementType = elementType;
                    t.As<JsTypeFunction>().GetTypeFromType = Jsni.function(() => Type._GetTypeFromTypeFunc(Jsni.@this().As<JsTypeFunction>()).As<JsObject>());
                    t.As<JsTypeFunction>().CreateTypeField = Jsni.function(() =>
                    {
                        var lastIndex = elementType.TypeName.LastIndexOf('.');
                        if (lastIndex == -1)
                            lastIndex = 0;
                        else
                            lastIndex++;
                        var type = new Type(elementType.TypeName.Substring(lastIndex) + "[]", new Attribute[0]);
                        arrayType.Type = type;
                        type.Init(
                            elementType.TypeName + "[]", 
                            0,
                            elementType, 
                            Jsni.type<Array>(), 
                            typeof(Array).interfaces.Concat(new[] { SpecialFunctions.MakeGenericTypeFactory(Jsni.type(typeof(IEnumerable<>)), Jsni.array(elementType)) }).ToArray(), 
                            new JsTypeFunction[0],
                            new FieldInfo[0], 
                            new MethodInfo[0], 
                            new ConstructorInfo[0], 
                            new PropertyInfo[0], 
                            new EventInfo[0], 
                            elementType,
                            null);
                        type.thisType = arrayType;
                        return type.As<JsObject>();
                    });
                }, SpecialNames.TypeInitializerTypeFunction, SpecialNames.TypeInitializerPrototype);
                Jsni.call(arrayType.TypeInitializer, Jsni.@this(), arrayType, arrayType.prototype);
                var result = arrayType;
                elementType.ArrayType = result;
            }
            return elementType.ArrayType;
        }

        [Js(Name = SpecialNames.NullableGetValue)]
        public static object NullableGetValue(object o)
        {
            if (o == null)
                throw new InvalidOperationException("Nullable object must have a value.");
            return o;
        }

        [Js(Name = SpecialNames.SafeToString)]
        public static string SafeToString(object o)
        {
            return o == null ? "" : Jsni._typeof(o.As<JsObject>()) == "boolean" ? o.As<JsObject>().toString().As<string>() : o.As<JsObject>().member("ToString") != null ? o.ToString() : o.As<JsObject>().toString().As<string>();
        }

        [Js(Name = SpecialNames.Truncate)]
        public static JsNumber Truncate(JsNumber number)
        {
            return number < 0 ? JsMath.ceil(number) : JsMath.floor(number);
        }

        [Js(Name = SpecialNames.DefaultOf)]
        public static JsObject DefaultOf(JsTypeFunction type)
        {
            var typeName = type.TypeName;
            switch (typeName)
            {
                case "System.Boolean":
                    return false;
                case "System.Byte":
                case "System.SByte":
                case "System.Int16":
                case "System.Int32":
                case "System.Int64":
                case "System.UInt16":
                case "System.UInt32":
                case "System.UInt64":
                case "System.Single":
                case "System.Double":
                case "System.Decimal":
                    return 0.As<JsNumber>();
                default:
                    return null;
            }
        }

        [Js(Name = SpecialNames.SafeGetType)]
        public static Type SafeGetType(object o)
        {
            var jsObject = o.As<JsObject>();
            if (jsObject.member("GetType") != null)
                return jsObject.member("GetType").invoke().As<Type>();

            var type = Jsni._typeof(jsObject);
            if (type == "boolean")
                return typeof(bool);
            if (type == "number")
                return typeof(Number);

            throw new Exception("Unable to determine type of: " + o);
        }

        [Js(Name = "$fastarraycopy")]
        public static T[] FastArrayCopy<T>(T[] array)
        {
            JsFunction function = Jsni.procedure(() => {});
            function.prototype = array.As<JsObject>();
            return Jsni.@new(function).As<T[]>();
        }
    }
}
