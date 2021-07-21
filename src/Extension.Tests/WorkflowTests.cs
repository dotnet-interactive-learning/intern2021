﻿
using Extension.Tests.Utilities;
using FluentAssertions;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.CSharp;
using Microsoft.DotNet.Interactive.Events;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Extension.Tests
{
    public class WorkflowTests : ProgressiveLearningTestBase
    {
        private IReadOnlyList<SendEditableCode> sampleContent = new SendEditableCode[]
        {
            new SendEditableCode("markdown",
@"# Challenge 1

## Add 1 with 2 and return it"),

            new SendEditableCode("csharp",
@"// write your answer here")
        };

        private string sampleAnswer =
@"#!csharp
1 + 2";

        private IReadOnlyList<SendEditableCode> sampleContent2 = new SendEditableCode[]
        {
            new SendEditableCode("markdown",
@"# Challenge 2

## Times 1 with 2 and return it
"),

            new SendEditableCode("csharp", @"
// write your answer here
")
        };

        [Fact]
        public async Task Test()
        {
            var lesson = new Lesson();
            using var kernel = CreateKernel(lesson);
            using var events = kernel.KernelEvents.ToSubscribedList();

            // teacher defines challenge
            var challenge1 = new Challenge(sampleContent);
            var challenge2 = new Challenge(sampleContent2);
            challenge1.AddRule(ruleContext =>
            {
                ruleContext.Fail("this rule failed because reasons");
            });
            challenge1.OnCodeSubmittedAsync(async challengeContext =>
            {
                var numPassed = challengeContext.RuleEvaluations.Count(e => e.Passed);
                var total = challengeContext.RuleEvaluations.Count();
                if (numPassed / total >= 0.5)
                {
                    challengeContext.SetMessage("Good work! Challenge 1 passed.");
                    await challengeContext.StartChallengeAsync(challenge2);
                }
                else
                {
                    challengeContext.SetMessage("Keep working!");
                }
            });
            lesson.AddChallenge(challenge1);
            lesson.AddChallenge(challenge2);

            // teacher sends challenge
            await lesson.StartLessonAsync();

            // student submit code
            await kernel.SubmitCodeAsync("1+1");

            events.Should().ContainSingle<DisplayedValueProduced>()
                .Which.FormattedValues
                .Should()
                .ContainSingle(v =>
                    v.MimeType == "text/html"
                    && v.Value.Contains("Keep working!")
                    && v.Value.Contains("this rule failed because reasons"));
        }

        [Fact]
        public async Task teacher_can_access_challenge_submission_history_for_challenge_evaluation()
        {
            var lesson = new Lesson();
            using var kernel = CreateKernel(lesson);
            using var events = kernel.KernelEvents.ToSubscribedList();

            // teacher defines challenge
            var challenge1 = new Challenge(sampleContent);
            challenge1.AddRule(ruleContext =>
            {
                ruleContext.Fail("this rule failed because reasons");
            });
            challenge1.OnCodeSubmitted(challengeContext =>
            {
                var history = challengeContext.SubmissionHistory;
                var pastConsecFailures = 0;
                foreach (var submission in history)
                {
                    var numPassed = submission.RuleEvaluations.Count(e => e.Passed);
                    var total = submission.RuleEvaluations.Count();
                    if (numPassed / total < 0.5)
                    {
                        pastConsecFailures++;
                    }
                    else
                    {
                        pastConsecFailures = 0;
                    }
                }

                if (pastConsecFailures > 2)
                {
                    challengeContext.SetMessage("Enough! Try something else.");
                }
                else
                {
                    var numPassed = challengeContext.RuleEvaluations.Count(e => e.Passed);
                    var total = challengeContext.RuleEvaluations.Count();
                    if (numPassed / total >= 0.5)
                    {
                        challengeContext.SetMessage("Good work! Challenge 1 passed.");
                    }
                    else
                    {
                        challengeContext.SetMessage("Keep working!");
                    }
                }
            });
            lesson.AddChallenge(challenge1);

            // teacher sends challenge
            await lesson.StartLessonAsync();

            // student submit code
            await kernel.SubmitCodeAsync(sampleAnswer);
            await kernel.SubmitCodeAsync(sampleAnswer);
            await kernel.SubmitCodeAsync(sampleAnswer);
            await kernel.SubmitCodeAsync(sampleAnswer);

            events
                .Should()
                .ContainSingle<DisplayedValueProduced>(
                    e => e.FormattedValues.Single(
                        v => v.MimeType == "text/html").Value.Contains("Enough! Try something else."));
        }
    }
}
