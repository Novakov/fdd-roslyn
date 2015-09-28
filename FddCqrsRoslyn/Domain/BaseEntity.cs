using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    [NHibernate]
    public abstract class BaseEntity
    {
        public abstract Guid Id { get; protected set; }
    }
}
