using HdbscanSharp.Hdbscanstar;
using System;
using System.Collections.Generic;

namespace HdbscanSharp.Runner
{
    public interface IHdbscanRunner
    {
        /// <summary>
        /// Runs HDBSCAN over a typed dataset.
        /// </summary>
        /// <param name="clusterSelectionMethod">
        /// Flat cluster extraction strategy.
        /// Use <see cref="ClusterSelectionMethod.Eom"/> for prominent/persistent clusters (default),
        /// or <see cref="ClusterSelectionMethod.Leaf"/> for finer, purity-first leaf clusters.
        /// </param>
        public HdbscanResult<A> Run<A, B>(
            List<A> dataset,
            Func<A, B> getVector,
            int minPoints,
            int minClusterSize,
            Func<B[], Func<int, int, double>> getDistanceFunc,
            List<HdbscanConstraint> constraints = null,
            double clusterSelectionEpsilon = 0,
            ClusterSelectionMethod clusterSelectionMethod = ClusterSelectionMethod.Eom
        );

        /// <summary>
        /// Runs HDBSCAN over an indexed dataset.
        /// </summary>
        /// <param name="clusterSelectionMethod">
        /// Flat cluster extraction strategy.
        /// Use <see cref="ClusterSelectionMethod.Eom"/> for prominent/persistent clusters (default),
        /// or <see cref="ClusterSelectionMethod.Leaf"/> for finer, purity-first leaf clusters.
        /// </param>
        public HdbscanResult Run(
            int datasetCount,
            int minPoints,
            int minClusterSize,
            Func<int, int, double> distanceFunc,
            List<HdbscanConstraint> constraints = null,
            double clusterSelectionEpsilon = 0,
            ClusterSelectionMethod clusterSelectionMethod = ClusterSelectionMethod.Eom);
    }

    public class HdbscanRunnerInstance : IHdbscanRunner
    {
        public HdbscanResult<A> Run<A, B>(
            List<A> dataset,
            Func<A, B> getVector,
            int minPoints,
            int minClusterSize,
            Func<B[], Func<int, int, double>> getDistanceFunc,
            List<HdbscanConstraint> constraints = null,
            double clusterSelectionEpsilon = 0,
            ClusterSelectionMethod clusterSelectionMethod = ClusterSelectionMethod.Eom)
        {
            return HdbscanRunner.Run(dataset, getVector, minPoints, minClusterSize, getDistanceFunc, constraints, clusterSelectionEpsilon, clusterSelectionMethod);
        }

        public HdbscanResult Run(
            int datasetCount,
            int minPoints,
            int minClusterSize,
            Func<int, int, double> distanceFunc,
            List<HdbscanConstraint> constraints = null,
            double clusterSelectionEpsilon = 0,
            ClusterSelectionMethod clusterSelectionMethod = ClusterSelectionMethod.Eom)
        {
            return HdbscanRunner.Run(datasetCount, minPoints, minClusterSize, distanceFunc, constraints, clusterSelectionEpsilon, clusterSelectionMethod);
        }
    }

    public class HdbscanRunner
    {
        /// <summary>
        /// Runs HDBSCAN over a typed dataset.
        /// </summary>
        /// <param name="clusterSelectionMethod">
        /// Flat cluster extraction strategy.
        /// Use <see cref="ClusterSelectionMethod.Eom"/> for prominent/persistent clusters (default),
        /// or <see cref="ClusterSelectionMethod.Leaf"/> for finer, purity-first leaf clusters.
        /// </param>
        public static HdbscanResult<A> Run<A, B>(
            List<A> dataset,
            Func<A, B> getVector,
            int minPoints,
            int minClusterSize,
            Func<B[], Func<int, int, double>> getDistanceFunc,
            List<HdbscanConstraint> constraints = null,
            double clusterSelectionEpsilon = 0,
            ClusterSelectionMethod clusterSelectionMethod = ClusterSelectionMethod.Eom
        )
        {
            var vectors = new B[dataset.Count];
            for (var i = 0; i < dataset.Count; i++)
            {
                vectors[i] = getVector(dataset[i]);
            }
            var distanceFunc = getDistanceFunc(vectors);
            var result = Run(dataset.Count, minPoints, minClusterSize, distanceFunc, constraints, clusterSelectionEpsilon, clusterSelectionMethod);
            var groups = new SortedDictionary<int, List<A>>();
            for (var i = 0; i < result.Labels.Length; i++)
            {
                var label = result.Labels[i];
                if (!groups.TryGetValue(label, out var list))
                {
                    list = new List<A>();
                    groups[label] = list;
                }
                list.Add(dataset[i]);
            }

            var outliersScore = new List<OutlierScore<A>>(result.OutliersScore.Count);
            for (var i = 0; i < result.OutliersScore.Count; i++)
            {
                var outlierScore = result.OutliersScore[i];
                outliersScore.Add(new OutlierScore<A>(
                    outlierScore.Score, outlierScore.CoreDistance, dataset[i]));
            }

            return new HdbscanResult<A>
            {
                Groups = new Dictionary<int, List<A>>(groups),
                OutliersScore = outliersScore,
                HasInfiniteStability = result.HasInfiniteStability,
                RelativeValidity = result.RelativeValidity,
                ClusterPersistence = result.ClusterPersistence
            };
        }

        /// <summary>
        /// Runs HDBSCAN over an indexed dataset.
        /// </summary>
        /// <param name="clusterSelectionMethod">
        /// Flat cluster extraction strategy.
        /// Use <see cref="ClusterSelectionMethod.Eom"/> for prominent/persistent clusters (default),
        /// or <see cref="ClusterSelectionMethod.Leaf"/> for finer, purity-first leaf clusters.
        /// </param>
        public static HdbscanResult Run(
            int datasetCount,
            int minPoints,
            int minClusterSize,
            Func<int, int, double> distanceFunc,
            List<HdbscanConstraint> constraints = null,
            double clusterSelectionEpsilon = 0,
            ClusterSelectionMethod clusterSelectionMethod = ClusterSelectionMethod.Eom)
        {
            var numPoints = datasetCount;

            // Compute core distances
            var coreDistances = HdbscanAlgorithm.CalculateCoreDistances(
                distanceFunc,
                numPoints,
                minPoints);

            // Calculate minimum spanning tree
            var mst = HdbscanAlgorithm.ConstructMst(
                distanceFunc,
                numPoints,
                coreDistances,
                true);
            mst.QuicksortByEdgeWeight();

            var pointNoiseLevels = new double[numPoints];
            var pointLastClusters = new int[numPoints];
            var hierarchy = new List<int[]>();

            // Compute hierarchy and cluster tree
            var clusters = HdbscanAlgorithm.ComputeHierarchyAndClusterTree(
                mst,
                minClusterSize,
                constraints,
                hierarchy,
                pointNoiseLevels,
                pointLastClusters);

            // Propagate clusters
            var infiniteStability = HdbscanAlgorithm.PropagateTree(clusters);

            // Compute final flat partitioning
            var prominentClusters = HdbscanAlgorithm.FindProminentClusters(
                clusters,
                hierarchy,
                numPoints,
                clusterSelectionEpsilon,
                clusterSelectionMethod,
                out var selectedClusters);

            var clusterPersistence = HdbscanAlgorithm.CalculateClusterPersistence(selectedClusters);
            var relativeValidity = HdbscanAlgorithm.CalculateRelativeValidity(prominentClusters, clusterPersistence);

            // Compute outlier scores for each point
            var scores = HdbscanAlgorithm.CalculateOutlierScores(
                clusters,
                pointNoiseLevels,
                pointLastClusters,
                coreDistances);

            return new HdbscanResult
            {
                Labels = prominentClusters,
                OutliersScore = scores,
                HasInfiniteStability = infiniteStability,
                RelativeValidity = relativeValidity,
                ClusterPersistence = clusterPersistence
            };
        }
    }
}
