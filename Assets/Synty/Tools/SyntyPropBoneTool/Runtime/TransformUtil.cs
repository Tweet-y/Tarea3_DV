// Copyright (c) 2024 Synty Studios Limited. All rights reserved.
//
// Use of this software is subject to the terms and conditions of the Synty Studios End User Licence Agreement (EULA)
// available at: https://syntystore.com/pages/end-user-licence-agreement
using UnityEngine;

namespace Synty.Tools.SyntyPropBoneTool
{
    public static class TransformUtil
    {
        public static Transform SearchHierarchy(Transform current, string name)
        {
            if (current == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (current.name == name)
            {
                return current;
            }

            for (int i = 0; i < current.childCount; ++i)
            {
                Transform found = SearchHierarchy(current.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
