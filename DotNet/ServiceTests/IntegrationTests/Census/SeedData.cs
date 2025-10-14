using System.Diagnostics;
using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census;

public static class SeedData
{

        private static readonly Random _random = new Random();

        /// <summary>
        /// Generates a specified number of patient IDs
        /// </summary>
        /// <param name="count">Number of patient IDs to generate</param>
        /// <returns>List of patient IDs</returns>
        public static List<string> GeneratePatientIds(int count)
        {
            return Enumerable.Range(0, count)
                .Select(_ => Guid.NewGuid().ToString())
                .ToList();
        }

        /// <summary>
        /// Generates a list of facility IDs with a specified naming pattern
        /// </summary>
        /// <param name="count">Number of facilities to generate</param>
        /// <param name="prefix">Prefix for facility names</param>
        /// <returns>List of facility IDs</returns>
        public static List<string> GenerateFacilityIds(int count, string prefix = "Facility")
        {
            return Enumerable.Range(1, count)
                .Select(i => $"{prefix}{i}")
                .ToList();
        }

        /// <summary>
        /// Seeds the database with CensusConfig entities for testing
        /// </summary>
        /// <param name="db">CensusContext</param>
        /// <param name="facilityIds">List of facility IDs to create configs for</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task SeedCensusConfigs(CensusContext db, List<string> facilityIds)
        {
            foreach (var facilityId in facilityIds)
            {
                db.CensusConfigs.Add(new CensusConfigEntity
                {
                    FacilityID = facilityId,
                    ScheduledTrigger = "0 0 * * *"
                });
            }

            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Generates patient list items for testing, ensuring each facility has exactly 6 lists
        /// (3 admit lists and 3 discharge lists, one for each time frame), with a controlled number of patient events.
        /// </summary>
        /// <param name="facilityIds">List of facility IDs to generate patient lists for</param>
        /// <param name="patientIds">Pool of patient IDs to distribute across facilities</param>
        /// <param name="admitPercentage">Percentage of patients that will be admitted (0-100)</param>
        /// <param name="dischargePercentage">Percentage of admitted patients that will be discharged (0-100)</param>
        /// <param name="maxPatientsPerTimeframe">Maximum number of patients per timeframe to control total event count</param>
        /// <returns>Dictionary mapping facility IDs to their respective lists of patient list items</returns>
        public static Dictionary<string, List<PatientListItem>> GeneratePatientListItems(List<string> facilityIds,
            List<string> patientIds)
        {
            var result = facilityIds.ToDictionary(
                facilityId => facilityId,
                _ => new List<PatientListItem>());

            int patientsPerFacility = 65;

            var random = new Random(42); // Using fixed seed for reproducibility
            var timeframes = new[]
            {
                TimeFrame.LessThan24Hours,
                TimeFrame.Between24To48Hours,
                TimeFrame.MoreThan48Hours
            };

            // Distribute patients per facility
            foreach (var facilityId in facilityIds)
            {
                // Divide all patients into chunks for each facility
                // We need 65 patients per facility:
                // - 20 patients * 3 timeframes = 60 for admits 
                // - 5 more for discharge-only

                // Get a random subset of patients for this facility
                var facilityPatients = patientIds
                    .OrderBy(_ => random.Next()) // Shuffle
                    .Take(patientsPerFacility)
                    .ToList();

                // Create separate lists to track patients for different purposes
                var admitPatients = new Dictionary<TimeFrame, List<string>>();
                var dischargePatients = new Dictionary<TimeFrame, List<string>>();
                var dischargeOnlyPatients = new List<string>();

                // Allocate patients to lists
                int patientIndex = 0;

                // Allocate patients for admit lists (20 per timeframe)
                foreach (var timeframe in timeframes)
                {
                    var timeframeAdmitPatients = facilityPatients
                        .Skip(patientIndex)
                        .Take(20)
                        .ToList();

                    admitPatients[timeframe] = timeframeAdmitPatients;
                    patientIndex += 20;
                }

                // Allocate 5 patients for discharge-only (no corresponding admit)
                dischargeOnlyPatients = facilityPatients
                    .Skip(patientIndex)
                    .Take(5)
                    .ToList();

                // For each timeframe, select half of the admitted patients for discharge
                foreach (var timeframe in timeframes)
                {
                    // Take half (10) of the patients from the admit list for this timeframe
                    dischargePatients[timeframe] = admitPatients[timeframe]
                        .Take(10)
                        .ToList();
                }

                // Add admit lists to result
                foreach (var timeframe in timeframes)
                {
                    result[facilityId].Add(new PatientListItem
                    {
                        ListType = ListType.Admit,
                        TimeFrame = timeframe,
                        PatientIds = admitPatients[timeframe]
                    });
                }

                // Add discharge lists to result
                foreach (var timeframe in timeframes)
                {
                    // Combine patients to be discharged from admit list + discharge-only patients
                    var dischargeList = new List<string>(dischargePatients[timeframe]);

                    // Distribute the 5 discharge-only patients across the 3 timeframes
                    if (timeframe == TimeFrame.LessThan24Hours)
                    {
                        dischargeList.AddRange(dischargeOnlyPatients.Take(2));
                    }
                    else if (timeframe == TimeFrame.Between24To48Hours)
                    {
                        dischargeList.AddRange(dischargeOnlyPatients.Skip(2).Take(2));
                    }
                    else // MoreThan48Hours
                    {
                        dischargeList.AddRange(dischargeOnlyPatients.Skip(4).Take(1));
                    }

                    result[facilityId].Add(new PatientListItem
                    {
                        ListType = ListType.Discharge,
                        TimeFrame = timeframe,
                        PatientIds = dischargeList
                    });
                }
            }

            return result;
        }
        
        /// <summary>
        /// Process all generated patient lists through the PatientListService
        /// </summary>
        /// <param name="patientListService">Service to process the lists</param>
        /// <param name="facilityLists">Dictionary mapping facility IDs to their patient list items</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task ProcessAllLists(
            LantanaGroup.Link.Census.Application.Services.PatientListService patientListService,
            Dictionary<string, List<PatientListItem>> facilityLists,
            CancellationToken cancellationToken)
        {
            foreach (var facility in facilityLists)
            {
                var facilityId = facility.Key;
                var lists = facility.Value;

                // Process lists sequentially to simulate realistic time progression
                foreach (var list in lists)
                {
                    await patientListService.ProcessList(facilityId, list, cancellationToken);
                }
            }
        }

    
}