using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberQuery : IModel
    {
        [JsonProperty("select")]
        public AccSaberQuerySelect Select { get; set; } = null!;

        [JsonProperty("from")]
        public string From { get; set; } = null!;

        [JsonProperty("filters")]
        public List<AccSaberQueryFilter>? Filters { get; set; }

        [JsonProperty("having")]
        public AccSaberQueryHaving? Having { get; set; }


        public IEnumerable<string>? GetUnfulfilledMaps(IEnumerable<AccSaberLeaderboardEntry> scores, float targetValue = default)
        {
            if (!From.Equals("scores"))
                return null;

            int initialCount = scores.Count();

            if (Filters is not null)
                foreach (AccSaberQueryFilter filter in Filters)
                    scores = filter.FilterInvertAll(scores);

            IEnumerable<IComparable> scoreVals = Select.Select(scores, targetValue);

            List<(AccSaberLeaderboardEntry entry, IComparable val)> scoreList = [.. scores.Select(entry => (entry, (JObject.FromObject(entry)[Select.Column] as IComparable)!))];
            List<string> diffIds = [];

            foreach (IComparable val in scoreVals)
            {
                var entry = scoreList.First(score => score.val.CompareTo(val) == 0);
                scoreList.Remove(entry);
                diffIds.Add(entry.entry.DifficultyId);
            }

            return diffIds;
        }

    }

    [UsedImplicitly]
    internal class AccSaberQuerySelect : IModel
    {
        [JsonProperty("function")]
        public FunctionType Function { get; set; }

        [JsonProperty("column")]
        public string Column { get; set; } = null!;

        [JsonProperty("operator")]
        public string? Operator { get; set; }


        public IEnumerable<IComparable> Select<T>(IEnumerable<T> items, float targetValue = default) where T : notnull => Function.Execute(Column, items, targetValue);

    }

    [UsedImplicitly]
    internal class AccSaberQueryHaving : AccSaberQuerySelect
    {
        [JsonProperty("value_query")]
        public AccSaberQuery? ValueQuery { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberQueryFilter : IModel
    {
        [JsonProperty("column")]
        public string Column { get; set; } = null!;

        [JsonProperty("operator")]
        public string Operator { get; set; } = null!;

        [JsonIgnore]
        public ComparisonType OperatorType { get; set; }

        [JsonIgnore]
        public SpecifiedComparer Comparer { get; set; } = null!;

        [JsonIgnore]
        public Func<IComparable, bool> Predicate { get; set; } = null!;

        [JsonProperty("value")]
        public IComparable Value { get; set; } = null!;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            OperatorType = Operator.FromComparisonString();
            Comparer = OperatorType.ToComparison();
            Predicate = obj => Comparer(obj, Value);
        }

        public IEnumerable<T> Filter<T>(IEnumerable<T> collection) where T : IComparable => collection.Where(obj => Predicate(obj));
        public IEnumerable<T> FilterAll<T>(IEnumerable<T> collection) where T : notnull => collection.Where(item => Predicate((JObject.FromObject(item)[Column] as IComparable)!));
        public IEnumerable<T> FilterInvert<T>(IEnumerable<T> collection) where T : IComparable
        {
            SpecifiedComparer invertComparer = OperatorType.Invert().ToComparison();
            return collection.Where(obj => invertComparer(obj, Value));
        }
        public IEnumerable<T> FilterInvertAll<T>(IEnumerable<T> collection) where T : notnull
        {
            SpecifiedComparer invertComparer = OperatorType.Invert().ToComparison();
            return collection.Where(item => invertComparer((JObject.FromObject(item)[Column] as IComparable)!, Value));
        }
    }

}
