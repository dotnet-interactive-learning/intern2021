﻿using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension
{
    public class Challenge
    {
        public Lesson Lesson { get; set; }
        public IReadOnlyList<EditableCode> Contents { get; }
        public bool Revealed { get; set; } = false;
        public Func<ChallengeContext, Task> OnCodeSubmittedHandler { get; private set; }
        public ChallengeEvaluation CurrentEvaluation { get; private set; }
        public ChallengeSubmission CurrentSubmission => _submissionHistory.Peek();
        public IEnumerable<ChallengeSubmission> SubmissionHistory => _submissionHistory;

        private List<Rule> _rules = new();
        private Stack<ChallengeSubmission> _submissionHistory = new();
        private ChallengeContext _context;

        public Challenge(IReadOnlyList<EditableCode> content, Lesson lesson = null)
        {
            Contents = content;
            Lesson = lesson;
        }

        public async Task Evaluate(string submissionCode = null, IEnumerable<KernelEvent> events = null)
        {
            _context = new ChallengeContext(this);

            foreach (var (index, rule) in _rules.Select((r, i) => (i, r)))
            {
                var ruleContext = new RuleContext(_context, $"Rule {index + 1}");
                rule.Evaluate(ruleContext);
            }

            await InvokeOnCodeSubmittedHandler();
            
            _submissionHistory.Push(new ChallengeSubmission(submissionCode, _context.Evaluation, events));
        }

        // todo: rename
        public async Task InvokeOnCodeSubmittedHandler()
        {
            if (OnCodeSubmittedHandler != null)
            {
                await OnCodeSubmittedHandler(_context);
            }
        }

        public void AddRuleAsync(Func<RuleContext, Task> action)
        {
            AddRule(new Rule(action));
        }

        public void AddRule(Action<RuleContext> action)
        {
            AddRuleAsync((context) =>
            {
                action(context);
                return Task.CompletedTask;
            });
        }

        public void OnCodeSubmittedAsync(Func<ChallengeContext, Task> action)
        {
            OnCodeSubmittedHandler = action;
        }

        public void OnCodeSubmitted(Action<ChallengeContext> action)
        {
            OnCodeSubmittedAsync((context) =>
            {
                action(context);
                return Task.CompletedTask;
            });
        }

        private void AddRule(Rule rule)
        {
            _rules.Add(rule);
        }
    }
}
