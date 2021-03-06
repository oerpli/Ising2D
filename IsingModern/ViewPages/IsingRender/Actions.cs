﻿using System;
using System.Threading;

namespace IsingModern.ViewPages {
    public partial class IsingRender {
        private readonly Semaphore sem = new Semaphore(1, 5);
        private readonly Semaphore run = new Semaphore(1, 1);

        private void RandomizeLattice() {
            _viewmodel.Randomize(true);
        }

        private void SetLatticeTo(int? val = null) {
            _viewmodel.SetLatticeTo(val);
        }

        private void ScaleLattice() {
            _viewmodel.ScaleSize(CurrentN);
            UpdateHelpLines();
        }

        private void Boundary() {
            _viewmodel.SetBoundary(_periodicBoundary);
            BoundaryText.Text = _periodicBoundary ? "Periodic" : "Walled";
            UpdateHelpLines();
        }

        private void Reset() {
            ThreadedAction(SetLatticeTo); // no argument = random
            temperature = 1.00;
            magneticfield = 0.00;
            UpdateThumb(1.0, 0.0);
            TemperatureTextBox.Text = "1,00";
            MagnFieldTextBox.Text = "0,00";
            _periodicBoundary = true;
            _ferromagnetic = true;
            _singleFlip = true;
            ThreadedAction(Boundary);
            ChangeCoupling();
            ChangeAlgorithm();
            _viewmodel.ChangeTemperature(temperature);
            _viewmodel.ChangeField(magneticfield);
        }



        private void ChangeCoupling() {
            _viewmodel.ChangeCoupling(_ferromagnetic ? 1.0 : -1.0);
            CouplingText.Text = _ferromagnetic ? "Ferromagnetic" : "Anti-Ferromagnetic";
        }

        private void ChangeAlgorithm() {
            AlgorithmText.Text = _singleFlip ? "SingleFlip" : "Kawasaki";
            _viewmodel.ChangeDynamic(AlgorithmText.Text);
        }

        private void ThreadedAction(Action action) {
            sem.WaitOne();
            action();
            sem.Release();
        }
        private void ThreadedAction(Action<int?> action, int? p = null) {
            sem.WaitOne();
            action(p);
            sem.Release();
        }
    }
}
