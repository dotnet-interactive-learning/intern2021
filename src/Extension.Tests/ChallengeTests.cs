﻿using Extension.Tests.Utilities;
using FluentAssertions;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.CSharp;
using Microsoft.DotNet.Interactive.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Extension.Tests
{
    public class ChallengeTests
    {
        private Challenge GetEmptyChallenge(Lesson lesson = null)
        {
            return new Challenge(new EditableCode[] { }, lesson);
        }

        // todo:  this test should be changed to use end to end, this is prob too artificial
        [Fact]
        public async Task teacher_can_start_another_challenge_when_evaluating_a_challenge()
        {
            var lesson = new Lesson();
            var challenge1 = GetEmptyChallenge(lesson);
            var challenge2 = GetEmptyChallenge(lesson);
            challenge1.OnCodeSubmittedAsync(async (context) =>
            {
                await context.StartChallengeAsync(challenge2);
            });
            await challenge1.Evaluate();

            await challenge1.InvokeOnCodeSubmittedHandler();

            lesson.CurrentChallenge.Should().Be(challenge2);
        }

        [Fact]
        public async Task teacher_can_access_code_from_submission_history_when_evaluating_a_challenge()
        {
            var capturedCode = new List<string>();
            var lesson = new Lesson();
            using var kernel = new CompositeKernel
            {
                new CSharpKernel()
            }.UseLessonEvaluateMiddleware(lesson);
            var challenge = GetEmptyChallenge(lesson);
            await lesson.StartChallengeAsync(challenge);
            challenge.OnCodeSubmitted(context =>
            {
                capturedCode = context.SubmissionHistory.Select(h => h.SubmittedCode).ToList();
            });

            await kernel.SubmitCodeAsync("1 + 1");
            await kernel.SubmitCodeAsync("1 + 2");
            await kernel.SubmitCodeAsync("1 + 3");

            capturedCode.Should().BeEquivalentTo("1 + 2", "1 + 1");
            capturedCode.Should().NotContain("1 + 3");
        }

        [Fact]
        public async Task challenge_tracks_submitted_code_in_submission_history()
        {
            var lesson = new Lesson();
            using var kernel = new CompositeKernel
            {
                new CSharpKernel()
            }.UseLessonEvaluateMiddleware(lesson);
            var challenge = GetEmptyChallenge(lesson);
            await lesson.StartChallengeAsync(challenge);

            await kernel.SubmitCodeAsync("1 + 1");
            await kernel.SubmitCodeAsync("1 + 2");
            await kernel.SubmitCodeAsync("1 + 3");

            challenge.SubmissionHistory.Select(h => h.SubmittedCode).ToList().Should().BeEquivalentTo("1 + 3", "1 + 2", "1 + 1");
        }

        [Fact]
        public async Task challenge_tracks_events_in_submission_history()
        {
            var capturedEvents = new List<List<KernelEvent>>();
            var lesson = new Lesson();
            using var kernel = new CompositeKernel
            {
                new CSharpKernel()
            }.UseLessonEvaluateMiddleware(lesson);
            var challenge = GetEmptyChallenge(lesson);
            await lesson.StartChallengeAsync(challenge);

            await kernel.SubmitCodeAsync("alsjl");
            await kernel.SubmitCodeAsync("1 + 1");
            await kernel.SubmitCodeAsync("1 + 2");
            capturedEvents = challenge.SubmissionHistory.Select(s => s.EventsProduced.ToList()).ToList();
            capturedEvents.Should().SatisfyRespectively(new Action<List<KernelEvent>>[]
            {
                evts => evts.Should().ContainSingle<ReturnValueProduced>().Which.Value.Should().Be(3),
                evts => evts.Should().ContainSingle<ReturnValueProduced>().Which.Value.Should().Be(2),
                evts => evts.Should().ContainSingle<CommandFailed>()
            });
        }

        [Fact]
        public async Task challenge_tracks_evaluations_in_submission_history()
        {
            var capturedEvaluation = new List<ChallengeEvaluation>();
            var lesson = new Lesson();
            using var kernel = new CompositeKernel
            {
                new CSharpKernel()
            }.UseLessonEvaluateMiddleware(lesson);
            var challenge = GetEmptyChallenge(lesson);
            await lesson.StartChallengeAsync(challenge);
            int numberOfSubmission = 1;
            challenge.OnCodeSubmitted(context =>
            {
                context.SetMessage($"{numberOfSubmission}");
                numberOfSubmission++;
            });

            await kernel.SubmitCodeAsync("1 + 1");
            await kernel.SubmitCodeAsync("1 + 1");
            await kernel.SubmitCodeAsync("1 + 1");

            capturedEvaluation = challenge.SubmissionHistory.Select(h => h.Evaluation).ToList();

            capturedEvaluation.Should().SatisfyRespectively(new Action<ChallengeEvaluation>[]
            {
                e => e.Message.Should().Be("3"),
                e => e.Message.Should().Be("2"),
                e => e.Message.Should().Be("1")
            });
        }

        [Fact]
        public async Task teacher_can_access_code_when_evaluating_a_rule()
        {
            var capturedCode = new List<string>();
            var lesson = new Lesson();
            using var kernel = new CompositeKernel
            {
                new CSharpKernel()
            }.UseLessonEvaluateMiddleware(lesson);
            var challenge = GetEmptyChallenge(lesson);
            await lesson.StartChallengeAsync(challenge);
            challenge.AddRule(context =>
            {
                capturedCode.Add(context.SubmittedCode);
            });

            await kernel.SubmitCodeAsync("1 + 1");
            await kernel.SubmitCodeAsync("1 + 2");
            await kernel.SubmitCodeAsync("1 + 3");

            capturedCode.Should().BeEquivalentTo("1 + 1", "1 + 2", "1 + 3");
        }

        [Fact]
        public async Task teacher_can_access_events_when_evaluating_a_rule()
        {
            var capturedEvents = new List<List<KernelEvent>>();
            var lesson = new Lesson();
            using var kernel = new CompositeKernel
            {
                new CSharpKernel()
            }.UseLessonEvaluateMiddleware(lesson);
            var challenge = GetEmptyChallenge(lesson);
            await lesson.StartChallengeAsync(challenge);
            challenge.AddRule(context =>
            {
                capturedEvents.Add(context.EventsProduced.ToList());
            });

            await kernel.SubmitCodeAsync("alsjkdf");
            await kernel.SubmitCodeAsync("1 + 1");
            await kernel.SubmitCodeAsync("1 + 2");

            capturedEvents.Should().SatisfyRespectively(new Action<List<KernelEvent>>[]
            {
                evts => evts.Should().ContainSingle<CommandFailed>(),
                evts => evts.Should().ContainSingle<ReturnValueProduced>().Which.Value.Should().Be(2),
                evts => evts.Should().ContainSingle<ReturnValueProduced>().Which.Value.Should().Be(3)
            });
        }

        [Fact]
        public async Task teacher_can_use_assertion_libraries_in_rule_definitions()
        {
            var lesson = new Lesson();
            using var kernel = new CompositeKernel
            {
                new CSharpKernel()
            }.UseLessonEvaluateMiddleware(lesson);
            var challenge = GetEmptyChallenge(lesson);
            challenge.AddRule(c =>
            {
                3.Should().Be(10);
            });
            await lesson.StartChallengeAsync(challenge);

            await kernel.SubmitCodeAsync("1 + 1");

            challenge.CurrentEvaluation.RuleEvaluations.First().Reason.Should().Be("Expected value to be 10, but found 3.");
        }

        [Fact]
        public async Task teacher_can_user_exceptions_to_fail_evaluation()
        {
            var lesson = new Lesson();
            using var kernel = new CompositeKernel
            {
                new CSharpKernel()
            }.UseLessonEvaluateMiddleware(lesson);
            var challenge = GetEmptyChallenge(lesson);
            challenge.AddRule(c =>
            {
                throw new ArgumentException($"Students should write better than {c.SubmittedCode}");
            });
            await lesson.StartChallengeAsync(challenge);

            await kernel.SubmitCodeAsync("1 + 1");

            challenge.CurrentEvaluation.RuleEvaluations.First().Reason.Should().Be("Students should write better than 1 + 1");
        }

        [Fact]
        public async Task unhandled_exception_will_cause_rule_to_fail()
        {
            var lesson = new Lesson();
            using var kernel = new CompositeKernel
            {
                new CSharpKernel()
            }.UseLessonEvaluateMiddleware(lesson);
            var challenge = GetEmptyChallenge(lesson);
            challenge.AddRule(c =>
            {
                var userValue = 0;
                var ration = 10 / userValue;
                if (ration > 1)
                {
                    c.Pass("Good job");
                }
            });
            await lesson.StartChallengeAsync(challenge);

            await kernel.SubmitCodeAsync("1 + 1");

            challenge.CurrentEvaluation.RuleEvaluations.First().Reason.Should().Be("Attempted to divide by zero.");
        }

        [Fact]
        public async Task teacher_can_add_setup_code_to_a_challenge()
        {
            var lesson = new Lesson();
            using var kernel = new CompositeKernel
            {
                new CSharpKernel()
            }.UseLessonEvaluateMiddleware(lesson);
            using var events = kernel.KernelEvents.ToSubscribedList();
            var setup = new SubmitCode[] {
                new SubmitCode("var a = 2;"), 
                new SubmitCode("var b = 3;"),
                new SubmitCode("a = 4;")
            };
            var challenge = new Challenge(new EditableCode[] { }, lesson, setup);
            await lesson.StartChallengeAsync(challenge);

            await kernel.SubmitCodeAsync("a+b");

            events.Should().NotBeOfType<CommandFailed>();
            events.Should().ContainSingle<ReturnValueProduced>().Which.Value.Should().Be(7);

        }
    }
}

