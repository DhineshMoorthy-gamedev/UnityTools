using System;
using System.Collections.Generic;

namespace UnityTools.ObjectGrouper.Data
{
    [Serializable]
    public class GrouperData
    {
        public List<ObjectGroup> Groups = new List<ObjectGroup>();

        public void AddGroup(ObjectGroup group)
        {
            if (!Groups.Contains(group))
            {
                Groups.Add(group);
            }
        }

        public void RemoveGroup(ObjectGroup group)
        {
            if (Groups.Contains(group))
            {
                Groups.Remove(group);
            }
        }
    }
}
