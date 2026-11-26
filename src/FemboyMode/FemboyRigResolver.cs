using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// The installed rig can name the same bone differently from the
    /// hardcoded vanilla name. Prefer exact matching and allow indexed
    /// suffixes only for the same numeric bone family.
    /// </summary>
    internal static class FemboyRigResolver
    {
        internal static Transform FindBone(Transform root, string configuredName)
        {
            if (root == null || string.IsNullOrEmpty(configuredName))
                return null;

            Transform best = null;
            int bestRank = int.MaxValue;
            Transform[] bones = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < bones.Length; i++)
            {
                Transform item = bones[i];
                if (item == null) continue;

                int rank = FemboyRules.BoneMatchRank(configuredName, item.name);
                if (rank < 0 || rank >= bestRank)
                    continue;
                best = item;
                bestRank = rank;
                if (rank == 0) break;
            }
            return best;
        }
    }
}
