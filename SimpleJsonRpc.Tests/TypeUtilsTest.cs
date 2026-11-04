// using System.Reflection;
// using System.Text.Json;
// using System.Text.Json.Serialization.Metadata;
// using SimpleJsonRpc.Data;
// using SimpleJsonRpc.Delegates;
//
// namespace SimpleJsonRpc.Tests;
//
// public class TypeUtilsTest
// {
//     [Theory]
//     [InlineData(typeof(TypeUtilsTest))]
//     [InlineData(typeof(string))]
//     [InlineData(typeof(void))]
//     public void ExtractType_TypeIsNotTask_ReturnsSame(Type type)
//     {
//         Assert.Equal(type, TypeUtils.ExtractType(type));
//     }
//
//     [Theory]
//     [InlineData(typeof(Task), typeof(void))]
//     [InlineData(typeof(Task<string>), typeof(string))]
//     [InlineData(typeof(ValueTask), typeof(void))]
//     [InlineData(typeof(ValueTask<string>), typeof(string))]
//     [InlineData(typeof(ExtendedTask), typeof(void))]
//     [InlineData(typeof(TwiceExtendedTask), typeof(void))]
//     [InlineData(typeof(ExtendedGenericTask<string>), typeof(string))]
//     [InlineData(typeof(TwiceExtendedGenericTask<string>), typeof(string))]
//     public void ExtractType_TypeIsTask_UnpacksGenericType(Type inputType, Type outputType)
//     {
//         Assert.Equal(outputType, TypeUtils.ExtractType(inputType));
//     }
//
//     [Theory]
//     [InlineData(typeof(int))]
//     [InlineData(typeof(string))]
//     [InlineData(typeof(bool))]
//     [InlineData(typeof(float))]
//     [InlineData(typeof(double))]
//     [InlineData(typeof(decimal))]
//     [InlineData(typeof(byte))]
//     [InlineData(typeof(void))]
//     public void IsSerializable_Primitive_ReturnsTrue(Type type)
//     {
//         JsonSerializerOptions options = new()
//         {
//             TypeInfoResolver = new DefaultJsonTypeInfoResolver()
//         };
//         Assert.True(TypeUtils.IsSerializable(type, options));
//     }
//
//     [Theory]
//     [InlineData(typeof(Type))]
//     [InlineData(typeof(IntPtr))]
//     [InlineData(typeof(Action))]
//     [InlineData(typeof(MethodInfo))]
//     public void IsSerializable_Primitive_ReturnsFalse(Type type)
//     {
//         JsonSerializerOptions options = new()
//         {
//             TypeInfoResolver = new DefaultJsonTypeInfoResolver()
//         };
//         Assert.False(TypeUtils.IsSerializable(type, options));
//     }
//
//     [Theory]
//     [InlineData(typeof(int))]
//     [InlineData(typeof(string))]
//     [InlineData(typeof(bool))]
//     [InlineData(typeof(float))]
//     [InlineData(typeof(double))]
//     [InlineData(typeof(decimal))]
//     [InlineData(typeof(byte))]
//     public void IsDeserializable_Primitive_ReturnsTrue(Type type)
//     {
//         JsonSerializerOptions options = new()
//         {
//             TypeInfoResolver = new DefaultJsonTypeInfoResolver()
//         };
//         Assert.False(TypeUtils.IsDeserializable(type, options));
//     }
//
//     [Theory]
//     [InlineData(typeof(JsonRpcError))]
//     [InlineData(typeof(JsonRpcError<string>))]
//     public void IsDeserializable_Struct_ReturnsTrue(Type type)
//     {
//         JsonSerializerOptions options = new()
//         {
//             TypeInfoResolver = new DefaultJsonTypeInfoResolver()
//         };
//         Assert.True(TypeUtils.IsDeserializable(type, options));
//     }
//
//     [Theory]
//     [InlineData(typeof(Type))]
//     [InlineData(typeof(IntPtr))]
//     [InlineData(typeof(Action))]
//     [InlineData(typeof(MethodInfo))]
//     public void IsDeserializable_WeirdClass_ReturnsFalse(Type type)
//     {
//         JsonSerializerOptions options = new()
//         {
//             TypeInfoResolver = new DefaultJsonTypeInfoResolver()
//         };
//         Assert.False(TypeUtils.IsDeserializable(type, options));
//     }
//
//     #region Test class definitions
//
//     private abstract class ExtendedTask(Action action) : Task(action)
//     {
//     }
//
//     private abstract class TwiceExtendedTask(Action action) : ExtendedTask(action)
//     {
//     }
//
//     private abstract class ExtendedGenericTask<T>(Func<T> action) : Task<T>(action)
//     {
//     }
//
//     private abstract class TwiceExtendedGenericTask<T>(Func<T> action) : ExtendedGenericTask<T>(action)
//     {
//     }
//
//     #endregion
// }

