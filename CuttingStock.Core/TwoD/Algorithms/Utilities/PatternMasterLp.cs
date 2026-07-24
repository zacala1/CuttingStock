using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.LinearSolver;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>Continuous restricted master problem for 2D pattern columns.</summary>
    internal static class PatternMasterLp
    {
        public static bool Solve(
            List<PatternColumn> columns,
            int[] demand,
            out double[] multiplicities,
            out double[] duals)
        {
            multiplicities = Array.Empty<double>();
            duals = Array.Empty<double>();

            var solver = Solver.CreateSolver("GLOP");
            if (solver == null) return false;

            int orderCount = demand.Length;
            int columnCount = columns.Count;

            var variables = new Variable[columnCount];
            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                variables[columnIndex] = solver.MakeNumVar(
                    0.0,
                    double.PositiveInfinity,
                    $"x{columnIndex}");
            }

            var constraints = new Constraint[orderCount];
            for (int orderIndex = 0; orderIndex < orderCount; orderIndex++)
            {
                constraints[orderIndex] = solver.MakeConstraint(
                    demand[orderIndex],
                    double.PositiveInfinity,
                    $"d{orderIndex}");

                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    int count = columns[columnIndex].Counts[orderIndex];
                    if (count != 0)
                        constraints[orderIndex].SetCoefficient(variables[columnIndex], count);
                }
            }

            var objective = solver.Objective();
            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                objective.SetCoefficient(
                    variables[columnIndex],
                    columns[columnIndex].Sheet.Area);
            }
            objective.SetMinimization();

            var status = solver.Solve();
            if (status != Solver.ResultStatus.OPTIMAL &&
                status != Solver.ResultStatus.FEASIBLE)
            {
                return false;
            }

            multiplicities = variables.Select(variable => variable.SolutionValue()).ToArray();
            duals = constraints.Select(constraint => constraint.DualValue()).ToArray();
            return true;
        }
    }
}
