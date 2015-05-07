﻿using System.Collections.Generic;

namespace IsingModern.Model {
    public sealed partial class Lattice {
        public int N;
        private int Count { get; set; }
        public Spin[] Spins { get; private set; }


        public Lattice(int n, double averageMagnetization) {
            {
                Coupling = 1;
                Field = 0;
                Beta = 1;
                Dynamic = SingleFlip;
                Accept = MetropolisCached;
                Accepts = new Dictionary<string, AcceptanceFunction>()
                {
                    {"Metropolis", MetropolisCached}, 
                    {"Glauber", GlauberCached}
                };
                Dynamics = new Dictionary<string, DynamicsAlgorithm>()
                {
                    {"SingleFlip", SingleFlip}, 
                    {"Kawasaki", Kawasaki}
                };
            }
            N = n;
            var points = new Spin[N, N];
            Spins = new Spin[N * N];
            Count = 0;
            double seed = 1 - 0.5 * (averageMagnetization + 1);
            for(int i = 0; i < N; i++) {
                for(int j = 0; j < N; j++) {
                    var r = Rnd.NextDouble();
                    var val = r < seed ? -1 : 1;
                    Spins[Count] = points[i, j] = new Spin(val, Count);
                    Count++;
                }
            }
            InitializeNeighbours(points);
            SetBoundary(true);
        }

        private void InitializeNeighbours(Spin[,] points) {
            for(int i = 0; i < N; i++) {
                for(int j = 0; j < N; j++) {
                    var n = points[(i - 1 + N) % N, j];
                    var e = points[i, (j + 1) % N];
                    var s = points[(i + 1) % N, j];
                    var w = points[i, (j - 1 + N) % N];
                    points[i, j].SetNeighbours(n, e, w, s);
                }
            }
        }

        public void SetBoundary(bool periodic) {
            foreach(var p in Boundary) {
                p.Value = periodic ? -1 : 0;
            }
            UpdateStats();
        }

        private IEnumerable<Spin> Boundary {
            get {
                for(int i = 0; i < N; i++) {
                    for(int j = 0; j < N; j++) {
                        if(i == 0 || j == 0 || i == N - 1 || j == N - 1) {
                            yield return Spins[N * j + i];
                        }
                    }
                }
            }
        }


        internal Spin RandomSpin() {
            return Spins[Rnd.Next(N * N)];
        }

        public void Randomize() {
            foreach(var p in Spins) {
                if(p.Value != 0) {
                    p.Value = Rnd.NextDouble() > 0.5 ? -1 : 1;
                }
            }
            UpdateStats();
        }

        #region Hamiltonian




        #endregion
    }
}





