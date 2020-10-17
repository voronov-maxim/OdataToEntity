using System;
using System.Collections.Generic;
using System.Globalization;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public readonly struct Navigation
    {
        public Navigation(String constraintSchema, String dependentConstraintName, String principalConstraintName, String navigationName, bool isCollection)
        {
            ConstraintSchema = constraintSchema;
            DependentConstraintName = dependentConstraintName;
            PrincipalConstraintName = principalConstraintName;
            NavigationName = navigationName;
            IsCollection = isCollection;
        }

        public String ConstraintSchema { get; }
        public String DependentConstraintName { get; }
        public String NavigationName { get; }
        public bool IsCollection { get; }
        public String PrincipalConstraintName { get; }

        public static Dictionary<(String, String), List<Navigation>> GetNavigations(
            IReadOnlyList<ReferentialConstraint> referentialConstraints,
            IReadOnlyDictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> keyColumns,
            IReadOnlyDictionary<(String tableSchema, String tableName), String> tableFullNameEdmNames,
            IReadOnlyDictionary<(String tableSchema, String tableName), IReadOnlyList<NavigationMapping>> navigationMappings,
            IReadOnlyDictionary<(String tableSchema, String tableName), List<Column>> tableColumns)
        {
            var tableNavigations = new Dictionary<(String tableSchema, String tableName), List<Navigation>>();
            var navigationCounter = new Dictionary<(String, String, String), List<IReadOnlyList<KeyColumnUsage>>>();
            foreach (ReferentialConstraint fkey in referentialConstraints)
            {
                IReadOnlyList<KeyColumnUsage> dependentKeyColumns = keyColumns[(fkey.ConstraintSchema, fkey.ConstraintName)];

                KeyColumnUsage dependentKeyColumn = dependentKeyColumns[0];
                if (!tableFullNameEdmNames.TryGetValue((dependentKeyColumn.TableSchema, dependentKeyColumn.TableName), out String? principalEdmName))
                    continue;

                KeyColumnUsage principalKeyColumn = keyColumns[(fkey.UniqueConstraintSchema, fkey.UniqueConstraintName)][0];
                if (!tableFullNameEdmNames.TryGetValue((principalKeyColumn.TableSchema, principalKeyColumn.TableName), out String? dependentEdmName))
                    continue;

                bool selfReferences = false;
                String? dependentNavigationName = GetNavigationMappingName(navigationMappings, fkey, dependentKeyColumn);
                if (dependentNavigationName == null)
                {
                    selfReferences = dependentKeyColumn.TableSchema == principalKeyColumn.TableSchema && dependentKeyColumn.TableName == principalKeyColumn.TableName;
                    if (selfReferences)
                        dependentNavigationName = "Parent";
                    else
                        dependentNavigationName = Humanizer.InflectorExtensions.Singularize(dependentEdmName);

                    (String, String, String) dependentKey = (fkey.ConstraintSchema, dependentKeyColumn.TableName, principalKeyColumn.TableName);
                    if (navigationCounter.TryGetValue(dependentKey, out List<IReadOnlyList<KeyColumnUsage>>? columnsList))
                    {
                        if (FKeyExist(columnsList, dependentKeyColumns))
                            continue;

                        columnsList.Add(dependentKeyColumns);
                    }
                    else
                    {
                        columnsList = new List<IReadOnlyList<KeyColumnUsage>>() { dependentKeyColumns };
                        navigationCounter[dependentKey] = columnsList;
                    }

                    List<Column> dependentColumns = tableColumns[(dependentKeyColumn.TableSchema, dependentKeyColumn.TableName)];
                    dependentNavigationName = GetUniqueName(dependentColumns, dependentNavigationName, columnsList.Count);
                }

                String? principalNavigationName = GetNavigationMappingName(navigationMappings, fkey, principalKeyColumn);
                if (principalNavigationName == null)
                {
                    if (dependentKeyColumn.TableSchema == principalKeyColumn.TableSchema && dependentKeyColumn.TableName == principalKeyColumn.TableName)
                        principalNavigationName = "Children";
                    else
                        principalNavigationName = Humanizer.InflectorExtensions.Pluralize(principalEdmName);

                    (String, String, String) principalKey = (fkey.ConstraintSchema, principalKeyColumn.TableName, dependentKeyColumn.TableName);
                    if (navigationCounter.TryGetValue(principalKey, out List<IReadOnlyList<KeyColumnUsage>>? columnsList))
                    {
                        if (!selfReferences)
                        {
                            if (FKeyExist(columnsList, dependentKeyColumns))
                                continue;

                            columnsList.Add(dependentKeyColumns);
                        }
                    }
                    else
                    {
                        columnsList = new List<IReadOnlyList<KeyColumnUsage>>() { dependentKeyColumns };
                        navigationCounter[principalKey] = columnsList;
                    }

                    List<Column> principalColumns = tableColumns[(principalKeyColumn.TableSchema, principalKeyColumn.TableName)];
                    principalNavigationName = GetUniqueName(principalColumns, principalNavigationName, columnsList.Count);
                }

                AddNavigation(tableNavigations, fkey, dependentKeyColumn, dependentNavigationName, false);
                AddNavigation(tableNavigations, fkey, principalKeyColumn, principalNavigationName, true);
            }

            return tableNavigations;

            static void AddNavigation(Dictionary<(String tableSchema, String tableName), List<Navigation>> tableNavigations,
                ReferentialConstraint fkey, KeyColumnUsage keyColumn, String navigationName, bool isCollection)
            {
                if (!String.IsNullOrEmpty(navigationName))
                {
                    (String tableName, String tableSchema) tableFullName = (keyColumn.TableSchema, keyColumn.TableName);
                    if (!tableNavigations.TryGetValue(tableFullName, out List<Navigation>? principalNavigations))
                    {
                        principalNavigations = new List<Navigation>();
                        tableNavigations.Add(tableFullName, principalNavigations);
                    }
                    principalNavigations.Add(new Navigation(fkey.ConstraintSchema, fkey.ConstraintName, fkey.UniqueConstraintName, navigationName, isCollection));
                }
            }
            static bool FKeyExist(List<IReadOnlyList<KeyColumnUsage>> keyColumnsList, IReadOnlyList<KeyColumnUsage> keyColumns)
            {
                for (int i = 0; i < keyColumnsList.Count; i++)
                    if (keyColumnsList[i].Count == keyColumns.Count)
                    {
                        int j = 0;
                        for (; j < keyColumns.Count; j++)
                            if (keyColumnsList[i][j].ColumnName != keyColumns[j].ColumnName)
                                break;

                        if (j == keyColumns.Count)
                            return true;
                    }

                return false;
            }
            static int GetCountName(IReadOnlyList<Column> columns, String navigationName)
            {
                int counter = 0;
                for (int i = 0; i < columns.Count; i++)
                    if (String.Compare(navigationName, columns[i].ColumnName, StringComparison.OrdinalIgnoreCase) == 0)
                        counter++;
                return counter;
            }
            static String? GetNavigationMappingName(IReadOnlyDictionary<(String tableSchema, String tableName), IReadOnlyList<NavigationMapping>> navigationMappings,
                ReferentialConstraint fkey, KeyColumnUsage keyColumn)
            {
                if (navigationMappings.TryGetValue((keyColumn.TableSchema, keyColumn.TableName), out IReadOnlyList<NavigationMapping>? tableNavigationMappings))
                    for (int i = 0; i < tableNavigationMappings.Count; i++)
                    {
                        NavigationMapping navigationMapping = tableNavigationMappings[i];
                        if (String.CompareOrdinal(navigationMapping.ConstraintName, fkey.ConstraintName) == 0)
                            return navigationMapping.NavigationName;
                    }

                return null;
            }
            static String GetUniqueName(IReadOnlyList<Column> columns, String navigationName, int counter)
            {
                int counter2;
                String navigationName2 = navigationName;
                do
                {
                    counter2 = GetCountName(columns, navigationName2);
                    counter += counter2;
                    navigationName2 = counter > 1 ? navigationName + counter.ToString(CultureInfo.InvariantCulture) : navigationName;
                }
                while (counter2 > 0 && GetCountName(columns, navigationName2) > 0);
                return navigationName2;
            }
        }
    }
}
