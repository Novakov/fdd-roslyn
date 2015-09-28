using System;

namespace Domain
{       
    public class Person : BaseEntity
    {
        public override Guid Id
        {
            get
            {
                throw new NotImplementedException();
            } 

            protected set
            {
                throw new NotImplementedException();
            }
        }

        public string Name { get; set; }
    }
}
