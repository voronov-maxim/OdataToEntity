using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Mapping;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace OdataToEntity.Linq2Db
{
    public abstract class OeLinq2DbTable
    {
        public abstract int SaveDeleted(DataConnection dc);
        public abstract int SaveInserted(DataConnection dc);
        public abstract int SaveUpdated(DataConnection dc);
        public abstract void UpdateIdentities(PropertyDescriptor fkeyProperty, IDictionary<Object, Object> identities);

        public abstract IDictionary<Object, Object> Identities { get; }
    }

    public sealed class OeLinq2DbTable<T> : OeLinq2DbTable
    {
        private readonly List<T> _deleted;
        private readonly Dictionary<Object, Object> _identities;
        private readonly List<T> _inserted;
        private readonly List<T> _updated;

        public OeLinq2DbTable()
        {
            _deleted = new List<T>();
            _inserted = new List<T>();
            _identities = new Dictionary<Object, Object>();
            _updated = new List<T>();
        }
        public void Delete(T entity)
        {
            _deleted.Add(entity);
        }
        private static List<PropertyDescriptor> GetDatabaseGenerated()
        {
            var identityProperties = new List<PropertyDescriptor>();
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(typeof(T)))
                if (property.Attributes[typeof(IdentityAttribute)] != null)
                    identityProperties.Add(property);
            return identityProperties;
        }
        public void Insert(T entity)
        {
            _inserted.Add(entity);
        }
        public override int SaveDeleted(DataConnection dc)
        {
            foreach (T entity in Deleted)
                dc.Delete(entity);
            return Deleted.Count;
        }
        public override int SaveInserted(DataConnection dc)
        {
            List<PropertyDescriptor> identityProperties = GetDatabaseGenerated();
            if (identityProperties.Count == 0)
                foreach (T entity in Inserted)
                    dc.Insert(entity);
            else
                foreach (T entity in Inserted)
                {
                    Object identity = dc.InsertWithIdentity<T>(entity);
                    identity = Convert.ChangeType(identity, identityProperties[0].PropertyType);
                    Object old = identityProperties[0].GetValue(entity);
                    identityProperties[0].SetValue(entity, identity);
                    _identities.Add(old, identity);
                }
            return _inserted.Count;
        }
        public override int SaveUpdated(DataConnection dc)
        {
            foreach (T entity in Updated)
                dc.Update(entity);
            return Updated.Count;
        }
        public void Update(T entity)
        {
            _updated.Add(entity);
        }
        public override void UpdateIdentities(PropertyDescriptor fkeyProperty, IDictionary<Object, Object> identities)
        {
            foreach (T entity in Inserted)
            {
                Object oldValue = fkeyProperty.GetValue(entity);
                if (oldValue != null)
                {
                    Object newValue;
                    if (identities.TryGetValue(oldValue, out newValue))
                        fkeyProperty.SetValue(entity, newValue);
                }
            }
        }

        public IReadOnlyList<T> Deleted => _deleted;
        public override IDictionary<Object, Object> Identities => _identities;
        public IReadOnlyList<T> Inserted => _inserted;
        public IReadOnlyList<T> Updated => _updated;
    }
}
