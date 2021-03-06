﻿//
// SemantAPI.Robot, SemantAPI.Human
// Copyright (C) 2013 George Kozlov
// These programs are free software: you can redistribute them and/or modify them under the terms of the GNU General Public License as published by the Free Software Foundation. either version 3 of the License, or any later version.
// These programs are distributed in the hope that they will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details. You should have received a copy of the GNU General Public License along with this program. If not, see http://www.gnu.org/licenses/.
// For further questions or inquiries, please contact semantapi (at) gmail (dot) com
//

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml.Serialization;

using AlchemyAPI;

namespace SemantAPI.Common.Executors
{
	#region Serialization types

	[XmlRootAttribute("docSentiment", Namespace = "")]
	public sealed class AlchemyDocSentiment
	{
		[XmlElementAttribute("type")]
		public string Type { get; set; }

		[XmlElementAttribute("score")]
		public double Score { get; set; }

		[XmlElementAttribute("mixed")]
		public int Mixed { get; set; }
	}

	[XmlRootAttribute("results", Namespace = "")]
	public sealed class AlchemySentimentResult
	{
		[XmlElementAttribute("status")]
		public string Status { get; set; }

		[XmlElementAttribute("language")]
		public string Language { get; set; }

		[XmlElementAttribute("docSentiment", typeof(AlchemyDocSentiment))]
		public AlchemyDocSentiment SentimentDetails { get; set; }
	}

	#endregion

	public sealed class AlchemyExecutor : IExecutor
	{
		#region Private members

		AnalysisExecutionContext _context = null;
		IList<string> _languages = null;

		#endregion

		#region Constructor

		public AlchemyExecutor()
		{
			_languages = new List<string>() { "English", "French", "German", "Spanish", "Portuguese", "Italian" };
		}

		#endregion

		#region IExecutor membeers

		public void Execute(AnalysisExecutionContext context)
		{
			_context = context;

			if (context.Results.Count <= 0)
			{
				context.OnExecutionProgress("Alchemy", new AnalysisExecutionProgressEventArgs(AnalysisExecutionStatus.Canceled, 0, 0, 0));
				return;
			}

			AlchemyAPI.AlchemyAPI session = new AlchemyAPI.AlchemyAPI();
			session.SetAPIKey(context.Key);

			AlchemyAPI_TargetedSentimentParams parameters = new AlchemyAPI_TargetedSentimentParams();
			parameters.setOutputMode(AlchemyAPI_BaseParams.OutputMode.XML);

			int processed = 0;
			int failed = 0;
			foreach (KeyValuePair<string, ResultSet> document in context.Results)
			{
				try
				{
					string strResult = string.Empty;
					if (context.UseDebugMode)
					{
						TimeSpan time = TimeSpan.Zero;
						strResult = BenchmarkHelper.Invoke(new InvokeBenchmarkHandler(delegate(object state)
						{
							return session.TextGetTextSentiment(document.Value.Source, parameters);
						}), null, out time) as string;

						Console.WriteLine("\tAlchemyAPI: Sentiment for the document {0} has been retreived. Execution time is: {1}", document.Key, time.TotalMilliseconds);
					}
					else
						strResult = session.TextGetTextSentiment(document.Value.Source, parameters);

					using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(strResult)))
					{
						XmlSerializer serializer = new XmlSerializer(typeof(AlchemySentimentResult));
						AlchemySentimentResult result = (AlchemySentimentResult)serializer.Deserialize(stream);

						processed++;
						document.Value.AddOutput("Alchemy", result.SentimentDetails.Score, result.SentimentDetails.Type);

						AnalysisExecutionProgressEventArgs ea = new AnalysisExecutionProgressEventArgs(AnalysisExecutionStatus.Processed, context.Results.Count, processed, failed);
						context.OnExecutionProgress("Alchemy", ea);

						if (ea.Cancel)
							break;
					}
				}
				catch (Exception ex)
				{
					failed++;
					document.Value.AddOutput("Alchemy", 0, "failed");
					AnalysisExecutionProgressEventArgs ea = new AnalysisExecutionProgressEventArgs(AnalysisExecutionStatus.Failed, context.Results.Count, processed, failed);
					ea.Reason = ex.Message;
					context.OnExecutionProgress("Alchemy", ea);

					if (ea.Cancel)
						break;
				}
			}

			context.OnExecutionProgress("Alchemy", new AnalysisExecutionProgressEventArgs(AnalysisExecutionStatus.Success, context.Results.Count, processed, failed));
		}

		public AnalysisExecutionContext Context
		{
			get { return _context; }
		}

		public bool IsLanguageSupported(string language)
		{
			return _languages.Contains(language);
		}

		#endregion
	}
}
