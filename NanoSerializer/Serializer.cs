﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Reflection;
using NanoSerializer.Mappers;
using System.IO;

namespace NanoSerializer
{
    /// <summary>
    /// NanoSerializer is super fast and compact binary data contract serializer
    /// </summary>
    public sealed class Serializer
    {
        private static readonly List<TypeMapper> mappers = new List<TypeMapper>
        {
            new BoolMapper(),
            new ByteArrayMapper(),
            new ComplexMapper(),
            new DateTimeMapper(),
            new EnumMapper(),
            new IntMapper(),
            new ListStringMapper(),
            new LongMapper(),
            new StringMapper()
        };
        private readonly Dictionary<Type, List<TypeMapper>> runtime = new Dictionary<Type, List<TypeMapper>>();

        /// <summary>
        /// Register your custom type mapper
        /// </summary>
        /// <param name="mapper">Instance of your type mapper</param>
        public static void RegisterTypeMapper(TypeMapper mapper)
        {
            if (mapper is null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }
            mappers.Add(mapper);
        }


        /// <summary>
        /// Builds instance of serializer for all data contracts in type assembly
        /// </summary>
        /// <param name="type">Any type from your data contracts assembly</param>
        public Serializer(params Type[] types)
        {
            foreach (var typeItem in types)
            {
                Register(typeItem);
            }
        }

        private static Action<object, object> BuildSetAccessor(MethodInfo method)
        {
            var obj = Expression.Parameter(typeof(object), string.Empty);
            var value = Expression.Parameter(typeof(object));
            var convert = Expression.Convert(value, method.GetParameters()[0].ParameterType);
            var call = Expression.Call(Expression.Convert(obj, method.DeclaringType), method, convert);
            var expr = Expression.Lambda<Action<object, object>>(call, obj, value);
            return expr.Compile();
        }

        private static Func<object, object> BuildGetAccessor(MethodInfo method)
        {
            var obj = Expression.Parameter(typeof(object), string.Empty);
            var call = Expression.Call(Expression.Convert(obj, method.DeclaringType), method);
            var convert = Expression.Convert(call, typeof(object));
            var expr = Expression.Lambda<Func<object, object>>(convert, obj);
            return expr.Compile();
        }

        /// <summary>
        /// Builds serializer
        /// </summary>
        private void Register(Type type)
        {
            var builder = new List<TypeMapper>();

            var properties = type.GetRuntimeProperties()
                .Where(f => f.GetCustomAttribute<DataMemberAttribute>() != null)
                .OrderBy(f => f.GetCustomAttribute<DataMemberAttribute>().Order);

            foreach (var property in properties)
            {
                foreach (var mapperExisting in mappers)
                {
                    // Refactor it
                    var mapper = (TypeMapper)Activator.CreateInstance(mapperExisting.GetType());

                    if (mapper is ComplexMapper)
                    {
                        (mapper as ComplexMapper).Use(this);
                    }
                    if (mapper.Can(property.PropertyType))
                    {
                        var setter = BuildSetAccessor(property.SetMethod);

                        var getter = BuildGetAccessor(property.GetMethod);

                        mapper.Init(getter, setter);
                        
                        builder.Add(mapper);
                        break;
                    }
                }
            }

            runtime.Add(type, builder);
        }

        /// <summary>
        /// Serialize data contract to byte array
        /// </summary>
        /// <typeparam name="T">Serializable type</typeparam>
        /// <param name="source">Serializer build model</param>
        /// <param name="instance">Instance of serializable type</param>
        /// <returns>Byte array</returns>
        public void Serialize(object instance, Stream stream)
        {
            var initialPosition = stream.Position;
            var source = runtime[instance.GetType()];

            foreach(var mapper in source)
            {
                mapper.Get(instance, stream);
            }

            stream.Position = initialPosition;
        }

        /// <summary>
        /// Serialize data contract to byte array
        /// </summary>
        /// <typeparam name="T">Serializable type</typeparam>
        /// <param name="source">Serializer build model</param>
        /// <param name="instance">Instance of serializable type</param>
        /// <returns>Byte array</returns>
        public byte[] Serialize(object instance)
        {
            using (var stream = new MemoryStream())
            {
                Serialize(instance, stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserialize type from byte array
        /// </summary>
        /// <typeparam name="T">Serialization type</typeparam>
        /// <param name="data">Byte array</param>
        /// <returns>New instance of deserialized contract</returns>
        public T Deserialize<T>(ReadOnlySpan<byte> data) where T : new()
        {
            var instance = new T();

            Deserialize(instance, data);

            return instance;
        }

        internal void Deserialize(object instance, ReadOnlySpan<byte> data)
        {
            var source = runtime[instance.GetType()];

            var reader = new NanoReader
            {
                Instance = instance,
                Buffer = data,
                Position = 0
            };

            foreach (var mapper in source)
            {
                mapper.Set(ref reader);
            }
        }
    }
}
