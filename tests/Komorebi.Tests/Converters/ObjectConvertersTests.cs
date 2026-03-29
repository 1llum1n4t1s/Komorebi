using System.Globalization;

using Komorebi.Converters;

namespace Komorebi.Tests.Converters
{
    public class ObjectConvertersTests
    {
        #region IsTypeOf - Convert

        [Fact]
        public void IsTypeOf_NullValue_ReturnsFalse()
        {
            var converter = ObjectConverters.IsTypeOf;
            var result = converter.Convert(null, typeof(bool), typeof(string), CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        [Fact]
        public void IsTypeOf_NullParameter_ReturnsFalse()
        {
            var converter = ObjectConverters.IsTypeOf;
            var result = converter.Convert("hello", typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        [Fact]
        public void IsTypeOf_BothNull_ReturnsFalse()
        {
            var converter = ObjectConverters.IsTypeOf;
            var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        [Fact]
        public void IsTypeOf_ExactTypeMatch_ReturnsTrue()
        {
            var converter = ObjectConverters.IsTypeOf;
            var result = converter.Convert("hello", typeof(bool), typeof(string), CultureInfo.InvariantCulture);
            Assert.Equal(true, result);
        }

        [Fact]
        public void IsTypeOf_IntType_ReturnsTrue()
        {
            var converter = ObjectConverters.IsTypeOf;
            var result = converter.Convert(42, typeof(bool), typeof(int), CultureInfo.InvariantCulture);
            Assert.Equal(true, result);
        }

        [Fact]
        public void IsTypeOf_DerivedType_ReturnsTrue()
        {
            var converter = ObjectConverters.IsTypeOf;
            // ArgumentException derives from Exception
            var exception = new ArgumentException("test");
            var result = converter.Convert(exception, typeof(bool), typeof(Exception), CultureInfo.InvariantCulture);
            Assert.Equal(true, result);
        }

        [Fact]
        public void IsTypeOf_InterfaceMatch_ReturnsTrue()
        {
            var converter = ObjectConverters.IsTypeOf;
            var list = new System.Collections.Generic.List<int>();
            var result = converter.Convert(list, typeof(bool), typeof(System.Collections.IEnumerable), CultureInfo.InvariantCulture);
            Assert.Equal(true, result);
        }

        [Fact]
        public void IsTypeOf_TypeMismatch_ReturnsFalse()
        {
            var converter = ObjectConverters.IsTypeOf;
            var result = converter.Convert("hello", typeof(bool), typeof(int), CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        [Fact]
        public void IsTypeOf_ObjectType_AlwaysTrue()
        {
            var converter = ObjectConverters.IsTypeOf;
            var result = converter.Convert("hello", typeof(bool), typeof(object), CultureInfo.InvariantCulture);
            Assert.Equal(true, result);
        }

        #endregion

        #region IsTypeOf - ConvertBack

        [Fact]
        public void IsTypeOf_ConvertBack_ReturnsNotImplementedException()
        {
            var converter = ObjectConverters.IsTypeOf;
            var result = converter.ConvertBack("value", typeof(object), null, CultureInfo.InvariantCulture);
            Assert.IsType<NotImplementedException>(result);
        }

        #endregion
    }
}
