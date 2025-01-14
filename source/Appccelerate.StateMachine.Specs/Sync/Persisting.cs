//-------------------------------------------------------------------------------
// <copyright file="Persisting.cs" company="Appccelerate">
//   Copyright (c) 2008-2019 Appccelerate
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
// </copyright>
//-------------------------------------------------------------------------------

namespace Appccelerate.StateMachine.Specs.Sync
{
    using System;
    using System.Collections.Generic;
    using FakeItEasy;
    using FluentAssertions;
    using Infrastructure;
    using Machine;
    using Persistence;
    using Specs;
    using StateMachine.Extensions;
    using Xbehave;

    public class Persisting
    {
#pragma warning disable SA1602 // Enumeration items should be documented
        public enum State
        {
            A, B, S, S1, S2
        }

        public enum Event
        {
            B, X, S2, S
        }
#pragma warning restore SA1602 // Enumeration items should be documented

        [Scenario]
        public void Loading(
            StateMachineSaver<State> saver,
            StateMachineLoader<State> loader,
            FakeExtension extension,
            State sourceState,
            State targetState)
        {
            "establish a saved state machine with history".x(() =>
            {
                var stateMachineDefinitionBuilder = new StateMachineDefinitionBuilder<State, Event>();
                SetupStates(stateMachineDefinitionBuilder);
                var machine = stateMachineDefinitionBuilder
                    .WithInitialState(State.A)
                    .Build()
                    .CreatePassiveStateMachine();

                machine.Start();
                machine.Fire(Event.S2); // set history of super state S
                machine.Fire(Event.B);  // set current state to B

                saver = new StateMachineSaver<State>();
                loader = new StateMachineLoader<State>();

                machine.Save(saver);
            });

            "when state machine is loaded".x(() =>
            {
                loader.SetCurrentState(saver.CurrentStateId);
                loader.SetHistoryStates(saver.HistoryStates);

                var stateMachineDefinitionBuilder = new StateMachineDefinitionBuilder<State, Event>();
                SetupStates(stateMachineDefinitionBuilder);
                var loadedMachine = stateMachineDefinitionBuilder
                    .WithInitialState(State.A)
                    .Build()
                    .CreatePassiveStateMachine();

                extension = new FakeExtension();
                loadedMachine.AddExtension(extension);

                loadedMachine.Load(loader);

                loadedMachine.TransitionCompleted += (sender, args) =>
                    {
                        sourceState = args.StateId;
                        targetState = args.NewStateId;
                    };

                loadedMachine.Start();
                loadedMachine.Fire(Event.S);
            });

            "it should reset current state".x(() =>
                sourceState
                    .Should()
                    .Be(State.B));

            "it should reset all history states of super states".x(() =>
                targetState
                    .Should()
                    .Be(State.S2));

            "it should notify extensions".x(()
                => extension
                    .LoadedCurrentState
                    .Should()
                    .BeEquivalentTo(Initializable<State>.Initialized(State.B)));
        }

        [Scenario]
        public void LoadingNonInitializedStateMachine(
            PassiveStateMachine<State, Event> loadedMachine)
        {
            "when a not started state machine is loaded".x(() =>
            {
                var loader = new StateMachineLoader<State>();
                loader.SetCurrentState(Initializable<State>.UnInitialized());
                loader.SetHistoryStates(new Dictionary<State, State>());

                var stateMachineDefinitionBuilder = new StateMachineDefinitionBuilder<State, Event>();
                SetupStates(stateMachineDefinitionBuilder);
                loadedMachine = stateMachineDefinitionBuilder
                    .WithInitialState(State.A)
                    .Build()
                    .CreatePassiveStateMachine();
                loadedMachine.Load(loader);
            });

            "it should not be initialized already".x(() =>
            {
                var stateMachineSaver = new StateMachineSaver<State>();
                loadedMachine.Save(stateMachineSaver);
                stateMachineSaver
                    .CurrentStateId
                    .IsInitialized
                    .Should()
                    .BeFalse();
            });
        }

        [Scenario]
        public void LoadingAnInitializedStateMachine(
            PassiveStateMachine<string, int> machine,
            Exception receivedException)
        {
            "establish a started state machine".x(() =>
            {
                var stateMachineDefinitionBuilder = new StateMachineDefinitionBuilder<string, int>();
                stateMachineDefinitionBuilder.In("initial");
                machine = stateMachineDefinitionBuilder
                    .WithInitialState("initial")
                    .Build()
                    .CreatePassiveStateMachine();
                machine.Start();
            });

            "when state machine is loaded".x(() =>
                receivedException = Catch.Exception(() => machine.Load(A.Fake<IStateMachineLoader<string>>())));

            "it should throw invalid operation exception".x(() =>
            {
                receivedException.Should().BeOfType<InvalidOperationException>();
                receivedException.Message.Should().Be(ExceptionMessages.StateMachineIsAlreadyInitialized);
            });
        }

        [Scenario]
        public void InitialStateSetViaDefinitionBuilder()
        {
            var stateMachineSaver = new StateMachineSaver<string>();

            var stateMachineDefinitionBuilder = new StateMachineDefinitionBuilder<string, int>();
            stateMachineDefinitionBuilder
                .In("A")
                .On(1)
                .Goto("B");
            stateMachineDefinitionBuilder
                .In("B")
                .On(2)
                .Goto("C");
            stateMachineDefinitionBuilder
                .WithInitialState("A");
            var machine = stateMachineDefinitionBuilder
                .Build()
                .CreatePassiveStateMachine();

            machine.Start();

            machine.Fire(1);

            machine.Save(stateMachineSaver);

            stateMachineSaver
                .CurrentStateId
                .Should()
                .Match<Initializable<string>>(currentState =>
                    currentState.IsInitialized
                    && currentState.ExtractOrThrow() == "B");
        }

        private static void SetupStates(StateMachineDefinitionBuilder<State, Event> builder)
        {
            builder
                .In(State.A)
                    .On(Event.S2).Goto(State.S2)
                    .On(Event.X);
            builder
                .In(State.B)
                    .On(Event.S).Goto(State.S)
                    .On(Event.X);
            builder
                .DefineHierarchyOn(State.S)
                    .WithHistoryType(HistoryType.Deep)
                    .WithInitialSubState(State.S1)
                    .WithSubState(State.S2);
            builder
                .In(State.S)
                    .On(Event.B).Goto(State.B);
        }

        public class FakeExtension : ExtensionBase<State, Event>
        {
            public List<IInitializable<State>> LoadedCurrentState { get; } = new List<IInitializable<State>>();

            public override void Loaded(
                IStateMachineInformation<State, Event> stateMachineInformation,
                IInitializable<State> loadedCurrentState,
                IReadOnlyDictionary<State, State> loadedHistoryStates)
            {
                this.LoadedCurrentState.Add(loadedCurrentState);
            }
        }

        public class StateMachineSaver<TState> : IStateMachineSaver<TState>
            where TState : IComparable
        {
            public IInitializable<TState> CurrentStateId { get; private set; }

            public IReadOnlyDictionary<TState, TState> HistoryStates { get; private set; }

            public void SaveCurrentState(IInitializable<TState> currentState)
            {
                this.CurrentStateId = currentState;
            }

            public void SaveHistoryStates(IReadOnlyDictionary<TState, TState> historyStates)
            {
                this.HistoryStates = historyStates;
            }
        }

        public class StateMachineLoader<TState> : IStateMachineLoader<TState>
            where TState : IComparable
        {
            private IInitializable<TState> currentState;
            private IReadOnlyDictionary<TState, TState> historyStates;

            public void SetCurrentState(IInitializable<TState> state)
            {
                this.currentState = state;
            }

            public void SetHistoryStates(IReadOnlyDictionary<TState, TState> states)
            {
                this.historyStates = states;
            }

            public IReadOnlyDictionary<TState, TState> LoadHistoryStates()
            {
                return this.historyStates;
            }

            public IInitializable<TState> LoadCurrentState()
            {
                return this.currentState;
            }
        }
    }
}