﻿using System;
using System.ComponentModel;
using System.Reflection;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Linq.Mapping;
using Lucene.Net.Util;
using NUnit.Framework;

namespace Lucene.Net.Linq.Tests.Mapping
{
    [TestFixture]
    public class NumericReflectionFieldMapperTests
    {
        private NumericReflectionFieldMapper<Sample> mapper;

        public class Sample
        {
            public Int32 Int { get; set; }
            public long Long { get; set; }
            public Complex Complex { get; set; }
        }

        public class Complex
        {
            public string Id { get; set; }
        }

        [Test]
        public void StoreLong()
        {
            mapper = new NumericReflectionFieldMapper<Sample>(typeof(Sample).GetProperty("Long"), true, null, TypeDescriptor.GetConverter(typeof(long)), "Long", NumericUtils.PRECISION_STEP_DEFAULT);

            var sample = new Sample {Long = 1234L};
            var document = new Document();
            mapper.CopyToDocument(sample, document);

            var field = (NumericField)document.GetFieldable("Long");
            Assert.That(field.TokenStreamValue().ToString(), Is.EqualTo("(numeric,valSize=64,precisionStep=4)"));
        }

        [Test]
        public void UsesPrecisionStep()
        {
            mapper = new NumericReflectionFieldMapper<Sample>(typeof(Sample).GetProperty("Int"), true, null, TypeDescriptor.GetConverter(typeof(int)), "Int", 128);

            var sample = new Sample { Long = 1234L };
            var document = new Document();
            mapper.CopyToDocument(sample, document);

            var field = (NumericField)document.GetFieldable("Int");
            Assert.That(field.TokenStreamValue().ToString(), Is.EqualTo("(numeric,valSize=32,precisionStep=128)"));
        }

        [Test]
        public void ConvertsFieldValue()
        {
            mapper = new NumericReflectionFieldMapper<Sample>(typeof(Sample).GetProperty("Int"), true, null, TypeDescriptor.GetConverter(typeof(int)), "Int", 128);

            var result = mapper.ConvertFieldValue(new Field("Int", "100", Field.Store.YES, Field.Index.NO));

            Assert.That(result, Is.EqualTo(100));
        }

        [Test]
        public void ConvertsFieldValueToNonValueType()
        {
            var valueTypeConverter = new SampleConverter();

            mapper = new NumericReflectionFieldMapper<Sample>(typeof(Sample).GetProperty("Complex"), true, valueTypeConverter, TypeDescriptor.GetConverter(typeof(int)), "Complex", 128);

            var result = mapper.ConvertFieldValue(new Field("Complex", "100", Field.Store.YES, Field.Index.NO));

            Assert.That(result, Is.InstanceOf<Complex>());
        }

        public class SampleConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return sourceType == typeof (int);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                return new Complex {Id = value.ToString()};
            }
        }
    }
}