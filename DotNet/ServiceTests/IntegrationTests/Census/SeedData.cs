using System.Diagnostics;
using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
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

        public static async Task SeedPatientEvents(CensusContext db)
        {
            // Create 5 facility IDs
            var facilityIds = new List<string>
            {
                "Facility1", "Facility2", "Facility3", "Facility4", "Facility5"
            };

            // Distribution percentages for each facility (unevenly distributed)
            var facilityDistribution = new Dictionary<string, double>
            {
                { "Facility1", 0.35 }, // 35% of patients
                { "Facility2", 0.25 }, // 25% of patients
                { "Facility3", 0.20 }, // 20% of patients
                { "Facility4", 0.15 }, // 15% of patients
                { "Facility5", 0.05 } // 5% of patients
            };

            // Generate 5000 unique patient IDs
            var patientIds = Enumerable.Range(1, 5000)
                .Select(_ => Guid.NewGuid().ToString())
                .ToList();

            // Base date for September 2025
            var baseSeptember2025 = new DateTime(2025, 9, 1);
            var random = new Random(42); // Fixed seed for reproducibility

            Console.WriteLine("Creating 5000 patient admits...");

            // Dictionary to keep track of admit dates for each patient to ensure discharges come after
            var patientAdmitDates = new Dictionary<string, DateTime>();
            var patientsWithDischarge = new HashSet<string>(); // Track which patients have been discharged

            // Phase 1: Create 5000 patient admits distributed across facilities
            for (int i = 0; i < patientIds.Count; i++)
            {
                var patientId = patientIds[i];

                // Determine which facility based on distribution
                string facilityId = DeterminePatientFacility(facilityIds, facilityDistribution, random);

                // Random date in first 20 days of September 2025
                var admitDate = baseSeptember2025.AddDays(random.Next(20));
                patientAdmitDates[patientId] = admitDate;

                var correlationId = Guid.NewGuid().ToString();

                // Create admit event
                var apayload = new FHIRListAdmitPayload(patientId, admitDate);
                var admitEvent = apayload.CreatePatientEvent(facilityId, correlationId);
                await db.PatientEvents.AddAsync(admitEvent);

                // Save every 500 entries to avoid memory issues
                if (i % 500 == 0)
                {
                    await db.SaveChangesAsync();
                    Console.WriteLine($"Created {i} admit events...");
                }
            }

            await db.SaveChangesAsync();
            Console.WriteLine("Created all 5000 initial admit events.");

            // Phase 2: Create discharges for 4000 of the patients
            Console.WriteLine("Creating 4000 discharge events...");

            // Select 4000 patients randomly for discharge
            var patientsToDischarge = patientIds
                .OrderBy(_ => random.Next())
                .Take(4000)
                .ToList();

            for (int i = 0; i < patientsToDischarge.Count; i++)
            {
                var patientId = patientsToDischarge[i];
                var admitDate = patientAdmitDates[patientId];

                // Discharge date should be 1-10 days after admit date but still in September
                var maxDischargeOffset = Math.Min(10, (new DateTime(2025, 9, 30) - admitDate).Days);
                var dischargeOffset = random.Next(1, maxDischargeOffset + 1);
                var dischargeDate = admitDate.AddDays(dischargeOffset);

                // Find the facility this patient was admitted to
                var admitEvent = db.PatientEvents
                    .FirstOrDefault(pe => pe.SourcePatientId == patientId && pe.EventType == EventType.FHIRListAdmit);

                if (admitEvent == null)
                    continue;

                var facilityId = admitEvent.FacilityId;
                var correlationId = Guid.NewGuid().ToString();

                // Create discharge event
                var dpayload = new FHIRListDischargePayload(patientId, dischargeDate);
                var dischargeEvent = dpayload.CreatePatientEvent(facilityId, correlationId);
                await db.PatientEvents.AddAsync(dischargeEvent);

                patientsWithDischarge.Add(patientId);

                // Save every 500 entries
                if (i % 500 == 0)
                {
                    await db.SaveChangesAsync();
                    Console.WriteLine($"Created {i} discharge events...");
                }
            }

            await db.SaveChangesAsync();
            Console.WriteLine("Created all 4000 discharge events.");

            // Phase 3: Create another 500 admits for patients who were discharged
            Console.WriteLine("Creating 500 additional admits for previously discharged patients...");

            var patientsForReadmission = patientsWithDischarge
                .OrderBy(_ => random.Next())
                .Take(500)
                .ToList();

            for (int i = 0; i < patientsForReadmission.Count; i++)
            {
                var patientId = patientsForReadmission[i];

                // Find the patient's discharge event to get the discharge date
                var dischargeEvent = db.PatientEvents
                    .Where(pe => pe.SourcePatientId == patientId && pe.EventType == EventType.FHIRListDischarge)
                    .OrderByDescending(pe => pe.CreateDate) // Get the most recent discharge
                    .FirstOrDefault();

                if (dischargeEvent == null)
                    continue;

                // Parse discharge date from the event payload
                var dischargePayload = dischargeEvent.Payload as FHIRListDischargePayload;
                var dischargeDate = dischargePayload?.DischargeDate ??
                                    (DateTime)dischargeEvent.GetType()
                                        .GetProperty("EventDate")
                                        .GetValue(dischargeEvent);

                // Readmit date should be 1-7 days after discharge but still in September
                var maxReadmitOffset = Math.Min(7, (new DateTime(2025, 9, 30) - dischargeDate).Days);
                var readmitOffset = random.Next(1, maxReadmitOffset + 1);
                var readmitDate = dischargeDate.AddDays(readmitOffset);

                // Only proceed if readmit date is still in September
                if (readmitDate.Month == 9 && readmitDate.Year == 2025)
                {
                    var facilityId = dischargeEvent.FacilityId;
                    var correlationId = Guid.NewGuid().ToString();

                    // Create readmit event
                    var apayload = new FHIRListAdmitPayload(patientId, readmitDate);
                    var readmitEvent = apayload.CreatePatientEvent(facilityId, correlationId);
                    await db.PatientEvents.AddAsync(readmitEvent);
                }

                // Save every 100 entries
                if (i % 100 == 0)
                {
                    await db.SaveChangesAsync();
                    Console.WriteLine($"Created {i} readmit events...");
                }
            }

            await db.SaveChangesAsync();
            Console.WriteLine("Created 500 readmission events.");
            Console.WriteLine(
                "Total events created: 5000 initial admits + 4000 discharges + 500 readmits = 9500 events");
        }

        // Helper method to determine which facility a patient should be assigned to based on distribution
        private static string DeterminePatientFacility(List<string> facilityIds,
            Dictionary<string, double> facilityDistribution,
            Random random)
        {
            var randomValue = random.NextDouble();
            double cumulativeProbability = 0;

            foreach (var facility in facilityIds)
            {
                cumulativeProbability += facilityDistribution[facility];
                if (randomValue <= cumulativeProbability)
                    return facility;
            }

            // Default to the last facility if something goes wrong with probabilities
            return facilityIds.Last();
        }
}