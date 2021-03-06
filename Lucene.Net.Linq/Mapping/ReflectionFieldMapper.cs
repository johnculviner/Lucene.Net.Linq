﻿using System;
using System.ComponentModel;
using System.Reflection;
using Lucene.Net.Documents;
using Lucene.Net.Linq.Util;
using Lucene.Net.Search;

namespace Lucene.Net.Linq.Mapping
{
    internal interface IFieldMapper<in T> : IFieldMappingInfo
    {
        void CopyFromDocument(Document source, float score, T target);
        void CopyToDocument(T source, Document target);
    }

    internal interface IFieldMappingInfo
    {
        string FieldName { get; }
        TypeConverter Converter { get; }
        PropertyInfo PropertyInfo { get; }
        bool IsNumericField { get; }
        int SortFieldType { get; }
        bool CaseSensitive { get; }
        string ConvertToQueryExpression(object value);
    }

    internal class ReflectionFieldMapper<T> : IFieldMapper<T>
    {
        protected readonly PropertyInfo propertyInfo;
        protected readonly StoreMode store;
        protected readonly IndexMode index;
        protected readonly TypeConverter converter;
        protected readonly string fieldName;
        protected readonly bool caseSensitive;

        public ReflectionFieldMapper(PropertyInfo propertyInfo, StoreMode store, IndexMode index, TypeConverter converter, string fieldName, bool caseSensitive)
        {
            this.propertyInfo = propertyInfo;
            this.store = store;
            this.index = index;
            this.converter = converter;
            this.fieldName = fieldName;
            this.caseSensitive = caseSensitive;
        }

        public PropertyInfo PropertyInfo
        {
            get { return propertyInfo; }
        }

        public StoreMode Store
        {
            get { return store; }
        }

        public IndexMode IndexMode
        {
            get { return index; }
        }

        public TypeConverter Converter
        {
            get { return converter; }
        }

        public string FieldName
        {
            get { return fieldName; }
        }

        public bool CaseSensitive
        {
            get { return caseSensitive; }
        }

        public virtual bool IsNumericField { get { return false; } }

        public virtual int SortFieldType { get { return (Converter != null) ? -1 : SortField.STRING; } }

        public virtual void CopyFromDocument(Document source, float score, T target)
        {
            var field = source.GetField(fieldName);

            if (field == null) return;

            if (!propertyInfo.CanWrite) return;

            var fieldValue = ConvertFieldValue(field);

            propertyInfo.SetValue(target, fieldValue, null);
        }

        public virtual void CopyToDocument(T source, Document target)
        {
            var value = propertyInfo.GetValue(source, null);

            target.RemoveFields(fieldName);

            AddField(target, value);
        }

        public virtual string ConvertToQueryExpression(object value)
        {
            if (converter != null)
            {
                return (string)converter.ConvertTo(value, typeof(string));
            }

            return (string)value;
        }

        protected internal virtual object ConvertFieldValue(Field field)
        {
            var fieldValue = (object)field.StringValue();

            if (converter != null)
            {
                fieldValue = converter.ConvertFrom(fieldValue);
            }
            return fieldValue;
        }

        protected internal void AddField(Document target, object value)
        {
            if (value == null) return;

            var fieldValue = (string)null;

            if (converter != null)
            {
                fieldValue = (string)converter.ConvertTo(value, typeof(string));
            }
            else if (value is string)
            {
                fieldValue = (string)value;
            }

            if (fieldValue != null)
            {
                target.Add(new Field(fieldName, fieldValue, FieldStore, index.ToFieldIndex()));
            }
        }

        protected Field.Store FieldStore
        {
            get
            {
                switch(store)
                {
                    case StoreMode.Yes:
                        return Field.Store.YES;
                    case StoreMode.No:
                        return Field.Store.NO;
                    case StoreMode.Compress:
                        return Field.Store.COMPRESS;
                }
                throw new InvalidOperationException("Unrecognized FieldStore value " + store);
            }
        }
    }

    internal class ReflectionScoreMapper<T> : IFieldMapper<T>
    {
        private readonly PropertyInfo propertyInfo;

        public ReflectionScoreMapper(PropertyInfo propertyInfo)
        {
            this.propertyInfo = propertyInfo;
        }

        public void CopyToDocument(T source, Document target)
        {
        }

        public void CopyFromDocument(Document source, float score, T target)
        {
            propertyInfo.SetValue(target, score, null);
        }

        public string ConvertToQueryExpression(object value)
        {
            throw new NotImplementedException();
        }

        public int SortFieldType
        {
            get { return SortField.SCORE; }
        }

        public bool IsNumericField
        {
            get { throw new NotImplementedException(); }
        }

        public bool CaseSensitive
        {
            get { throw new NotImplementedException(); }
        }

        public PropertyInfo PropertyInfo
        {
            get { return propertyInfo; }
        }

        public TypeConverter Converter
        {
            get { throw new NotImplementedException(); }
        }

        public string FieldName
        {
            get { return null; }
        }
    }
}