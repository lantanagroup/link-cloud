using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Validators;

public static class PatientListsValidator
{
    //validate that the lists recieved follow this pattern
    //1. 6 lists
    //2. That the ListType/TimeFrame is unique
    //3. That the ListType/TimeFrame is valid
    public static (bool success, List<string> validationErrors) ValidatePatientLists(List<PatientListItem> lists)
    {
        if(lists.Count != 6)
            return (false, new List<string> { "Must have 6 lists" });
        
        Dictionary<(string, string), int> validLists = new();
        validLists.Add((ListType.Admit.ToString(), TimeFrame.Between24To48Hours.ToString()),0);
        validLists.Add((ListType.Discharge.ToString(), TimeFrame.Between24To48Hours.ToString()),0);
        validLists.Add((ListType.Admit.ToString(), TimeFrame.LessThan24Hours.ToString()),0);
        validLists.Add((ListType.Discharge.ToString(), TimeFrame.LessThan24Hours.ToString()),0);
        validLists.Add((ListType.Admit.ToString(), TimeFrame.MoreThan48Hours.ToString()),0);
        validLists.Add((ListType.Discharge.ToString(), TimeFrame.MoreThan48Hours.ToString()),0);

        foreach (var list in lists)
        {
            var key = (list.ListType.ToString(), list.TimeFrame.ToString());
            if(!validLists.ContainsKey(key))
                return (false, new List<string> { $"Invalid ListType/TimeFrame combination: {list.ListType}/{list.TimeFrame}" });
            validLists[key]++;
        }

        if (validLists.Values.Any(x => x > 1) || validLists.Values.Any(x => x == 0))
        {
            List<string> errors = new();
            foreach (var key in validLists.Keys)
            {
                if (validLists[key] > 1)
                    errors.Add($"Duplicate ListType/TimeFrame combination: {key.Item1}/{key.Item2}");
                else if (validLists[key] == 0)
                    errors.Add($"Invalid ListType/TimeFrame combination: {key.Item1}/{key.Item2}");
            }
            return (false, errors);
        }
        
        return (true, new List<string>());
    }
}