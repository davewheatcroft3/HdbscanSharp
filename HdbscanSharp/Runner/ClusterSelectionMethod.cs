namespace HdbscanSharp.Runner
{
    /// <summary>
    /// Cluster selection method used to extract a flat clustering from the condensed hierarchy.
    /// </summary>
    public enum ClusterSelectionMethod
    {
        /// <summary>
        /// Excess of Mass (EOM): prefers more persistent and prominent clusters.
        /// </summary>
        Eom = 0,

        /// <summary>
        /// Leaf selection: prefers finer, more homogeneous leaf clusters.
        /// </summary>
        Leaf = 1
    }
}
