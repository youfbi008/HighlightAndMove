﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Code2Xml.Core;
using Code2Xml.Core.Location;
using NUnit.Framework;

namespace SimilarHighlight.Tests
{
	[TestFixture]
    public class InferenceTest
    {
		[Test]
        [TestCase(@"../../../SimilarHighlight.Tests/InferenceTest.cs")]
		public void TestGetSimilarElements(string path) {
			var processor = ProcessorLoader.CSharpUsingAntlr3;	// processorIdentifier indicates here
			var fileInfo = new FileInfo(path);					// fileInfoIdentifier indicates here
			var code = File.ReadAllText(path);
			var xml = processor.GenerateXml(fileInfo);
			var elements = xml.Descendants("identifier").ToList();

			// Create locatoin information that user selects in the editor
			//
			// This test creates location information by analyzing ASTs
			// Actually, in real usage, you should create CodeRange instance
			// from locations that user selected
			//
			// You can create CodeRange instance from the location information,
			// that is, source code, a start index, and an end index
			// using CodeRange.ConvertFromIndicies()
			//

            var firstRange = CodeRange.Locate(elements.First(e => e.TokenText() == "processor"));
            var secondRange = CodeRange.Locate(elements.First(e => e.TokenText() == "fileInfo"));
			var processorIdentifier = new LocationInfo {
                CodeRange = firstRange,
                XElement = firstRange.FindOutermostElement(xml),
			};
			var fileInfoIdentifier = new LocationInfo {
                CodeRange = secondRange,
                XElement = secondRange.FindOutermostElement(xml),
			};

			// Get similar nodes
			var ret = Inferrer.GetSimilarElements(processor, new[] { processorIdentifier, fileInfoIdentifier },
                    xml);

			// Show the similar nodes
			foreach (var tuple in ret.Take(10)) {
				var score = tuple.Item1;
				var location = tuple.Item2;
				var startAndEnd = location.CodeRange.ConvertToIndicies(code);
                var fragment = code.Substring(startAndEnd.Item1, startAndEnd.Item2 - startAndEnd.Item1);
				Console.WriteLine("Similarity: " + score + ", code: " + fragment);
			}           
		}
    }
}