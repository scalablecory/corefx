using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Connections
{
    public interface IConnectionProperties
    {
        bool TryGet(Type propertyType, out object property);

        bool TryGet<TProperty>(out TProperty property)
        {
            if (TryGet(typeof(TProperty), out object obj))
            {
                property = (TProperty)obj;
                return true;
            }

            property = default;
            return false;
        }
    }
}
