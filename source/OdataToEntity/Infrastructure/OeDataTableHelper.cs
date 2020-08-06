using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.Infrastructure
{
    public static class OeDataTableHelper
    {
        public static DataTable GetDataTable(IEnumerable source)
        {
            Type itemType;
            if (source is IEnumerable<Object> list)
                itemType = list.First().GetType();
            else
                itemType = Parsers.OeExpressionHelper.GetCollectionItemType(source.GetType());

            if (Parsers.OeExpressionHelper.IsPrimitiveType(itemType))
                return GetPrimitiveDataTable(itemType, source);

            PropertyInfo[] properties = itemType.GetProperties();
            var table = new DataTable();
            foreach (PropertyInfo property in properties)
                table.Columns.Add(property.Name, property.PropertyType);
            Object?[] values = new Object[properties.Length];

            table.BeginLoadData();
            foreach (Object item in source)
            {
                for (int i = 0; i < properties.Length; i++)
                    values[i] = properties[i].GetValue(item, null);
                table.LoadDataRow(values, true);
            }
            table.EndLoadData();

            return table;
        }
        private static DataTable GetPrimitiveDataTable(Type itemType, IEnumerable source)
        {
            var table = new DataTable();
            table.Columns.Add("value", itemType);
            var values = new Object[1];

            table.BeginLoadData();
            foreach (Object item in source)
            {
                values[0] = item;
                table.LoadDataRow(values, true);
            }
            table.EndLoadData();

            return table;
        }
    }
}
