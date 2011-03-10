﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WatiN.Core;

namespace NQUnit
{
    public class QUnitParser : IDisposable
    {
        private readonly IE _ie;

        public QUnitParser()
        {
            _ie = new IE();
        }

        public IEnumerable<QUnitTest> GetQUnitTestResults(string testPage)
        {
            var fileName = Path.Combine(Environment.CurrentDirectory, "JavaScript", testPage);
            _ie.GoTo(fileName);
            _ie.WaitForComplete(5);

            return GrabTestResultsFromWebPage(testPage);
        }

        public IEnumerable<QUnitTest> GrabTestResultsFromWebPage(string testPage)
        {
            // BEWARE: This logic is tightly coupled to the structure of the HTML generated by the QUnit test runner
            // Also, this could probably be greatly simplified with a couple well-crafted XPath expressions
            var testOl = _ie.Elements.Filter(Find.ById("qunit-tests"))[0];
            if (testOl == null) yield break;
            var documentRoot = XDocument.Load(new StringReader(MakeXHtml(testOl.OuterHtml))).Root;
            if (documentRoot == null) yield break;

            foreach (var listItem in documentRoot.Elements())
            {
                var testName = listItem.Elements().First(x => x.Name.Is("strong")).Value;
                var resultClass = listItem.Attributes().First(x => x.Name.Is("class")).Value;
                var failedAssert = String.Empty;
                if (resultClass == "fail")
                {
                    var specificAssertFailureListItem = listItem.Elements()
                        .First(x => x.Name.Is("ol")).Elements()
                        .First(x => x.Name.Is("li") && x.Attributes().First(a => a.Name.Is("class")).Value == "fail");
                    if (specificAssertFailureListItem != null)
                    {
                        failedAssert = specificAssertFailureListItem.Value;
                    }
                }

                yield return new QUnitTest
                {
                    FileName = testPage,
                    TestName = RemoveAssertCounts(testName),
                    Result = resultClass,
                    Message = failedAssert
                };
            }

        }

        private static string MakeXHtml(string html)
        {
            var replacer = new Regex(@"<([^ >]+)(.*?)>", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var innerReplacer = new Regex(@"(\s+.+?=)([^\s$]+)", RegexOptions.IgnoreCase);
            var h = replacer.Replace(html, match =>
                "<" + match.Groups[1] + innerReplacer.Replace(match.Groups[2].Value, innerMatch =>
                    innerMatch.Groups[2].Value.Contains("\"") ? innerMatch.Groups[1].Value + innerMatch.Groups[2].Value : innerMatch.Groups[1].Value + "\"" + innerMatch.Groups[2].Value + "\""
                ) + ">"
            );
            return h;
        }


        private static string RemoveAssertCounts(string fullTagText)
        {
            if (fullTagText == null) return String.Empty;
            int parenPosition = fullTagText.IndexOf('(');
            if (parenPosition > 0)
            {
                return fullTagText.Substring(0, parenPosition);
            }
            return fullTagText;
        }

        public void Dispose()
        {
            _ie.Close();
        }
    }
}