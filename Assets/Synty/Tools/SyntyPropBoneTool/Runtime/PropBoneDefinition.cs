// Copyright (c) 2024 Synty Studios Limited. All rights reserved.
//
// Use of this software is subject to the terms and conditions of the Synty Studios End User Licence Agreement (EULA)
// available at: https://syntystore.com/pages/end-user-licence-agreement
using System;
using UnityEngine;

namespace Synty.Tools.SyntyPropBoneTool
{
    [Serializable]
    public class PropBoneDefinition
    {
        public string parentBoneName;
        public string boneName;
        public string socketName;
        public Vector3 rotationOffset;
        public float scale = 1f;
        public string scaleCalculationBone1;
        public string scaleCalculationBone2;

        public override string ToString()
        {
            return $"Bone: {boneName}, Socket: {socketName}, Parent: {parentBoneName}";
        }
    }
}
