﻿//-------------------------------------------------------------------------------
// <copyright file="ExceptionThrowingActionTransitionFacts.cs" company="Appccelerate">
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

namespace Appccelerate.StateMachine.Facts.AsyncMachine.Transitions
{
    using System;
    using System.Threading.Tasks;
    using AsyncMachine;
    using FakeItEasy;
    using FluentAssertions;
    using StateMachine.AsyncMachine;
    using StateMachine.AsyncMachine.ActionHolders;
    using Xunit;

    public class ExceptionThrowingActionTransitionFacts : TransitionFactsBase
    {
        private Exception exception;

        public ExceptionThrowingActionTransitionFacts()
        {
            this.Source = Builder<States, Events>.CreateStateDefinition().Build();
            this.Target = Builder<States, Events>.CreateStateDefinition().Build();
            this.TransitionContext = Builder<States, Events>.CreateTransitionContext().WithState(this.Source).Build();

            this.TransitionDefinition.Source = this.Source;
            this.TransitionDefinition.Target = this.Target;

            this.exception = new Exception();

            this.TransitionDefinition.ActionsModifiable.Add(new ArgumentLessActionHolder(() => throw this.exception));
        }

        [Fact]
        public async Task CallsExtensionToHandleException()
        {
            var extension = A.Fake<IExtension<States, Events>>();

            this.ExtensionHost.Extension = extension;

            await this.Testee.Fire(this.TransitionDefinition, this.TransitionContext, this.LastActiveStateModifier);

            A.CallTo(() => extension.HandlingTransitionException(this.StateMachineInformation, this.TransitionDefinition, this.TransitionContext, ref this.exception)).MustHaveHappened();
            A.CallTo(() => extension.HandledTransitionException(this.StateMachineInformation, this.TransitionDefinition, this.TransitionContext, this.exception)).MustHaveHappened();
        }

        [Fact]
        public async Task ReturnsFiredTransitionResult()
        {
            var result = await this.Testee.Fire(this.TransitionDefinition, this.TransitionContext, this.LastActiveStateModifier);

            result.Fired.Should().BeTrue();
        }

        [Fact]
        public async Task NotifiesExceptionOnTransitionContext()
        {
            await this.Testee.Fire(this.TransitionDefinition, this.TransitionContext, this.LastActiveStateModifier);

            A.CallTo(() => this.TransitionContext.OnExceptionThrown(this.exception)).MustHaveHappened();
        }
    }
}