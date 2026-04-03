namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Shared clinical code tables used across all resource factories.
/// All factories draw from these pools for both Generate() (seed-driven)
/// and Create() (caller-supplied with gap-fill) workflows.
///
/// Clinical design principle: each "scenario" row ties together a primary
/// diagnosis, a set of plausible medications, a set of plausible procedures,
/// and an appropriate set of observations — so the resources for a single
/// patient form a coherent clinical story.
/// </summary>
public static class FhirGenerationCodes
{
    // -----------------------------------------------------------------------
    //  Patient demographics
    // -----------------------------------------------------------------------

    public static readonly (
        string Gender, string[] GivenNames, string FamilyName, string BirthDate,
        string RaceCode, string RaceDisplay, string EthCode, string EthDisplay,
        string MaritalCode, string MaritalDisplay,
        string Street, string City, string State, string Zip,
        string EmergencyContactName, string EmergencyContactPhone)[] Patients =
    [
        ("male",   ["Robert", "James"],   "Price",    "1958-04-12", "2106-3", "White",             "2186-5", "Not Hispanic or Latino", "M", "Married",  "412 Elm Street",       "Dallas",     "TX", "75201", "Patricia Price",  "+1 214-555-0191"),
        ("female", ["Sandra", "Lynn"],    "Nguyen",   "1972-09-23", "2028-9", "Asian",             "2186-5", "Not Hispanic or Latino", "M", "Married",  "88 Magnolia Drive",    "Houston",    "TX", "77001", "Minh Nguyen",     "+1 713-555-0142"),
        ("male",   ["James", "William"],  "Johnson",  "1945-11-07", "2054-5", "Black or Afr. Am.", "2186-5", "Not Hispanic or Latino", "W", "Widowed",  "301 Oak Avenue",       "Austin",     "TX", "73301", "Marcus Johnson",  "+1 512-555-0163"),
        ("female", ["Maria", "Elena"],    "Garcia",   "1988-03-17", "2106-3", "White",             "2135-2", "Hispanic or Latino",     "S", "Single",   "19 Cactus Road",       "San Antonio","TX", "78201", "Carlos Garcia",   "+1 210-555-0174"),
        ("other",  ["Casey"],             "Thompson", "1965-07-30", "2106-3", "White",             "2186-5", "Not Hispanic or Latino", "D", "Divorced", "7 Birch Lane",         "Fort Worth", "TX", "76101", "Morgan Thompson", "+1 817-555-0185"),
        ("male",   ["David", "Chen"],     "Lee",      "1953-01-14", "2028-9", "Asian",             "2186-5", "Not Hispanic or Latino", "M", "Married",  "550 River Bend Blvd",  "El Paso",    "TX", "79901", "Susan Lee",       "+1 915-555-0196"),
        ("female", ["Patricia"],          "Williams", "1979-05-22", "2106-3", "White",             "2186-5", "Not Hispanic or Latino", "M", "Married",  "24 Pecan Street",      "Lubbock",    "TX", "79401", "Gregory Williams","+1 806-555-0107"),
        ("male",   ["Michael", "Thomas"], "Brown",    "1991-08-08", "2054-5", "Black or Afr. Am.", "2186-5", "Not Hispanic or Latino", "S", "Single",   "99 Sunset Boulevard",  "Amarillo",   "TX", "79101", "Diane Brown",     "+1 806-555-0118"),
    ];

    // -----------------------------------------------------------------------
    //  Practitioners
    // -----------------------------------------------------------------------

    public static readonly (string Family, string Given, string Gender, string Npi, string Email, string Specialty)[] Practitioners =
    [
        ("Green",     "Erin",    "female", "1003456789", "Erin.Green@testhosp.example.com",      "Internal Medicine"),
        ("Schneider", "Leah",    "female", "1013456780", "Leah.Schneider@testhosp.example.com",  "Pulmonology"),
        ("Becker",    "Hannah",  "female", "1023456781", "Hannah.Becker@testhosp.example.com",   "Cardiology"),
        ("Reilly",    "Gabriel", "male",   "1033456782", "Gabriel.Reilly@testhosp.example.com",  "Nephrology"),
        ("Martinez",  "Carlos",  "male",   "1043456783", "Carlos.Martinez@testhosp.example.com", "Emergency Medicine"),
        ("Patel",     "Arjun",   "male",   "1053456784", "Arjun.Patel@testhosp.example.com",     "Hospitalist"),
    ];

    // -----------------------------------------------------------------------
    //  Clinical scenarios — each row is a coherent admission story
    //  Scenario drives: primary diagnosis, admit reason, medications, procedures
    // -----------------------------------------------------------------------

    public static readonly (
        string PrimaryDxSnomed, string PrimaryDxDisplay, string PrimaryDxIcd,
        string AdmitTypeCode, string AdmitTypeDisplay,
        string AdmitSourceCode, string AdmitSourceDisplay,
        string DischargeDispositionCode, string DischargeDispositionDisplay,
        string ServiceTypeCode, string ServiceTypeDisplay,
        string PriorityCode, string PriorityDisplay)[] ClinicalScenarios =
    [
        // 0 — Community-acquired pneumonia, emergency admission
        ("233604007", "Pneumonia (disorder)",                               "J18.9",  "183452005", "Emergency hospital admission",  "emd",  "From accident/emergency department", "home",    "Home",              "305",  "General Medicine",  "EM", "emergency"),
        // 1 — Acute decompensated heart failure, urgent admission
        ("84114007",  "Heart failure (disorder)",                           "I50.9",  "32485007",  "Hospital admission (procedure)", "hosp-trans", "Transferred from other hospital",     "snf",     "Skilled nursing facility", "303", "Cardiology",   "R",  "routine"),
        // 2 — Acute myocardial infarction, emergency admission
        ("57054005",  "Acute myocardial infarction (disorder)",             "I21.9",  "183452005", "Emergency hospital admission",  "emd",  "From accident/emergency department", "home",    "Home",              "306",  "Cardiothoracic Surgery", "EM", "emergency"),
        // 3 — COPD exacerbation, emergency admission
        ("195951007", "Acute exacerbation of chronic obstructive airways disease (disorder)", "J44.1", "183452005", "Emergency hospital admission", "emd", "From accident/emergency department", "home", "Home", "305", "Pulmonology", "EM", "emergency"),
        // 4 — Sepsis from urinary source, emergency admission
        ("10001005",  "Septicemia (disorder)",                              "A41.9",  "183452005", "Emergency hospital admission",  "emd",  "From accident/emergency department", "home",    "Home",              "305",  "General Medicine",  "EM", "emergency"),
        // 5 — Hip fracture, elective surgical admission
        ("700097003", "Fracture of bone of hip region (disorder)",          "S72.001A","32485007", "Hospital admission (procedure)", "gp",   "General practitioner referral",       "home",    "Home",              "308",  "Orthopaedics",      "R",  "routine"),
        // 6 — Acute renal failure, urgent admission
        ("14669001",  "Acute renal failure syndrome (disorder)",            "N17.9",  "32485007",  "Hospital admission (procedure)", "hosp-trans", "Transferred from other hospital",     "home",    "Home",              "310",  "Nephrology",        "R",  "routine"),
        // 7 — Ischaemic stroke, emergency admission
        ("422504002", "Ischemic stroke (disorder)",                         "I63.9",  "183452005", "Emergency hospital admission",  "emd",  "From accident/emergency department", "rehab",   "Inpatient rehabilitation", "320", "Neurology",    "EM", "emergency"),
    ];

    // -----------------------------------------------------------------------
    //  Observations with interpretation thresholds (criticalLow, normalLow,
    //  normalHigh, criticalHigh) — used to populate interpretation codes
    // -----------------------------------------------------------------------

    public static readonly (
        string Code, string Display, string Category,
        string Unit, double CritLow, double NormLow, double NormHigh, double CritHigh)[] Observations =
    [
        // Vital signs
        ("8867-4",  "Heart rate",                                              "vital-signs", "{beats}/min",      40,  60,   100, 150),
        ("8310-5",  "Body temperature",                                        "vital-signs", "Cel",              35.0, 36.1, 37.2, 40.0),
        ("59408-5", "Oxygen saturation by Pulse oximetry",                     "vital-signs", "%",                80,  95,   100, 100),
        ("8302-2",  "Body height",                                             "vital-signs", "cm",               0,   150,  200, 999),
        ("29463-7", "Body weight",                                             "vital-signs", "kg",               0,   50,   120, 999),
        ("55284-4", "Blood pressure systolic and diastolic",                   "vital-signs", "mm[Hg]",           0,   90,   140, 220),
        ("9279-1",  "Respiratory rate",                                        "vital-signs", "{breaths}/min",    4,   12,   20,  40),
        ("59576-9", "Body mass index (BMI) [Ratio]",                           "vital-signs", "kg/m2",            0,   18.5, 29.9, 60),
        // Laboratory — haematology
        ("718-7",   "Hemoglobin [Mass/volume] in Blood",                       "laboratory",  "g/dL",             5,   12.0, 17.5, 20),
        ("4544-3",  "Hematocrit [Volume Fraction] of Blood",                   "laboratory",  "%",                15,  36,   52,  65),
        ("6690-2",  "Leukocytes [#/volume] in Blood",                          "laboratory",  "10*3/uL",          1,   4.5,  11,  30),
        ("777-3",   "Platelets [#/volume] in Blood",                           "laboratory",  "10*3/uL",          20,  150,  400, 1000),
        // Laboratory — chemistry
        ("2160-0",  "Creatinine [Mass/volume] in Serum or Plasma",             "laboratory",  "mg/dL",            0,   0.6,  1.2, 10),
        ("2345-7",  "Glucose [Mass/volume] in Serum or Plasma",                "laboratory",  "mg/dL",            40,  70,   99,  500),
        ("2951-2",  "Sodium [Moles/volume] in Serum or Plasma",                "laboratory",  "mmol/L",           120, 136,  145, 160),
        ("2823-3",  "Potassium [Moles/volume] in Serum or Plasma",             "laboratory",  "mmol/L",           2.5, 3.5,  5.0, 6.5),
        ("2075-0",  "Chloride [Moles/volume] in Serum or Plasma",              "laboratory",  "mmol/L",           90,  98,   106, 120),
        ("1975-2",  "Bilirubin.total [Mass/volume] in Serum or Plasma",        "laboratory",  "mg/dL",            0,   0.2,  1.2, 15),
        ("1742-6",  "Alanine aminotransferase [Enzymatic activity/volume]",    "laboratory",  "U/L",              0,   7,    56,  500),
        ("6768-6",  "Alkaline phosphatase [Enzymatic activity/volume]",        "laboratory",  "U/L",              0,   44,   147, 1000),
        ("2093-3",  "Cholesterol [Mass/volume] in Serum or Plasma",            "laboratory",  "mg/dL",            0,   0,    200, 400),
        ("2089-1",  "Cholesterol in LDL [Mass/volume] in Serum or Plasma",     "laboratory",  "mg/dL",            0,   0,    100, 300),
        ("14646-4", "Cholesterol in HDL [Mass/volume] in Serum or Plasma",     "laboratory",  "mg/dL",            0,   40,   999, 999),
        ("2571-8",  "Triglycerides [Mass/volume] in Serum or Plasma",          "laboratory",  "mg/dL",            0,   0,    150, 1000),
        ("48643-1", "Glomerular filtration rate/1.73 sq M.predicted [Volume Rate/Area] in Serum, Plasma or Blood by Creatinine-based formula (MDRD)", "laboratory", "mL/min/{1.73_m2}", 0, 60, 999, 999),
        ("2532-0",  "Lactate dehydrogenase [Enzymatic activity/volume]",       "laboratory",  "U/L",              0,   122,  222, 1000),
        // Laboratory — microbiology / culture
        ("600-7",   "Bacteria identified in Blood by Culture",                 "laboratory",  "",                 0,   0,    0,   0),
        // Laboratory — coagulation
        ("5902-2",  "Prothrombin time (PT)",                                   "laboratory",  "s",                0,   11,   13,  30),
        ("6301-6",  "INR in Platelet poor plasma by Coagulation assay",        "laboratory",  "{INR}",            0,   0.9,  1.1, 5),
        // Laboratory — cardiac
        ("33762-6", "NT-proBNP [Mass/volume] in Serum or Plasma",              "laboratory",  "pg/mL",            0,   0,    125, 35000),
        ("10839-9", "Troponin I.cardiac [Mass/volume] in Serum or Plasma",     "laboratory",  "ng/mL",            0,   0,    0.04, 50),
    ];

    // -----------------------------------------------------------------------
    //  Conditions (secondary / comorbidities — primary dx comes from scenario)
    // -----------------------------------------------------------------------

    public static readonly (string Code, string Display, string IcdCode, string Category)[] Conditions =
    [
        ("44054006",  "Diabetes mellitus type 2 (disorder)",                    "E11.9",  "problem-list-item"),
        ("38341003",  "Hypertensive disorder, systemic arterial (disorder)",    "I10",    "problem-list-item"),
        ("13645005",  "Chronic obstructive lung disease (disorder)",            "J44.1",  "problem-list-item"),
        ("73211009",  "Diabetes mellitus (disorder)",                           "E11.9",  "problem-list-item"),
        ("414545008", "Ischemic heart disease (disorder)",                      "I25.10", "problem-list-item"),
        ("40055000",  "Chronic sinusitis (disorder)",                           "J32.9",  "problem-list-item"),
        ("49436004",  "Atrial fibrillation (disorder)",                         "I48.91", "problem-list-item"),
        ("195662009", "Acute renal failure syndrome (disorder)",                "N17.9",  "encounter-diagnosis"),
        ("59621000",  "Hypertension (disorder)",                                "I10",    "problem-list-item"),
        ("230690007", "Cerebrovascular accident (disorder)",                    "I63.9",  "encounter-diagnosis"),
        ("267102003", "Sore throat symptom (finding)",                          "J02.9",  "encounter-diagnosis"),
        ("386661006", "Fever (finding)",                                        "R50.9",  "encounter-diagnosis"),
        ("25064002",  "Headache (finding)",                                     "R51",    "encounter-diagnosis"),
        ("422587007", "Nausea (finding)",                                       "R11.0",  "encounter-diagnosis"),
        ("57676002",  "Joint pain (finding)",                                   "M79.3",  "problem-list-item"),
        ("73595000",  "Stress (finding)",                                       "Z73.3",  "problem-list-item"),
    ];

    // -----------------------------------------------------------------------
    //  Procedures — with associated condition reason for clinical coherence
    // -----------------------------------------------------------------------

    public static readonly (string Code, string Display, string ReasonCode, string ReasonDisplay, string BodySiteCode, string BodySiteDisplay, string OutcomeCode, string OutcomeDisplay)[] Procedures =
    [
        ("18286008",  "Catheterization of urinary bladder",   "10001005",  "Septicemia",                 "87953007", "Urinary tract structure",   "385669000", "Successful"),
        ("225358003", "Wound care management",                "700097003", "Fracture of bone of hip",    "68505006",  "Skin structure of lower leg","385669000","Successful"),
        ("40617009",  "Artificial respiration",               "195951007", "Acute COPD exacerbation",    "44567001",  "Tracheal structure",         "385669000","Successful"),
        ("431231003", "Dialysis procedure",                   "14669001",  "Acute renal failure",        "64033007",  "Kidney structure",           "385669000","Successful"),
        ("447996002", "Insertion of PICC",                    "10001005",  "Septicemia",                 "80248007",  "Elbow structure",            "385669000","Successful"),
        ("232717009", "Coronary artery bypass grafting",      "57054005",  "Acute myocardial infarction","80891009",  "Heart structure",            "385669000","Successful"),
        ("34068001",  "Heart valve replacement",              "84114007",  "Heart failure",              "80891009",  "Heart structure",            "385669000","Successful"),
        ("265764009", "Renal dialysis",                       "14669001",  "Acute renal failure",        "64033007",  "Kidney structure",           "385669000","Successful"),
        ("173171007", "Thoracentesis",                        "233604007", "Pneumonia",                  "39607008",  "Lung structure",             "385669000","Successful"),
        ("312581009", "Bone marrow biopsy",                   "59021001",  "Bone marrow disease",        "14016003",  "Bone marrow structure",      "385669000","Successful"),
        ("108290001", "Repair of aortic aneurysm",            "57054005",  "Acute myocardial infarction","15825003",  "Aortic structure",           "385669000","Successful"),
        ("69261000",  "Endotracheal intubation",              "195951007", "Acute COPD exacerbation",    "44567001",  "Tracheal structure",         "385669000","Successful"),
        ("392230005", "Echocardiography",                     "84114007",  "Heart failure",              "80891009",  "Heart structure",            "385669000","Successful"),
    ];

    // -----------------------------------------------------------------------
    //  Medications — with explicit frequency, PRN flag, and indication
    // -----------------------------------------------------------------------

    public static readonly (
        string RxCode, string Display, string RouteCode, string RouteDisplay,
        double DoseValue, string DoseUnit, int FreqPerDay, bool Prn,
        string IndicationSnomed, string IndicationDisplay)[] Medications =
    [
        ("1049502",  "Acetaminophen 325 MG Oral Tablet",              "26643006", "Oral route",          650,  "mg",    4, true,  "386661006", "Fever"),
        ("197696",   "Ceftriaxone 250 MG Injection",                  "47625008", "Intravenous route",   2000, "mg",    1, false, "233604007", "Pneumonia"),
        ("309362",   "Enoxaparin 40 MG/0.4 ML Injectable Solution",   "34206005", "Subcutaneous route",  40,   "mg",    1, false, "59557009",  "Deep vein thrombosis prophylaxis"),
        ("835829",   "Vancomycin 500 MG Injection",                   "47625008", "Intravenous route",   1500, "mg",    2, false, "10001005",  "Septicemia"),
        ("313002",   "Metoprolol succinate 50 MG Oral Tablet",        "26643006", "Oral route",          50,   "mg",    1, false, "84114007",  "Heart failure"),
        ("197361",   "Furosemide 40 MG Oral Tablet",                  "26643006", "Oral route",          40,   "mg",    2, false, "84114007",  "Heart failure"),
        ("308460",   "Lisinopril 10 MG Oral Tablet",                  "26643006", "Oral route",          10,   "mg",    1, false, "38341003",  "Hypertension"),
        ("312961",   "Amoxicillin 500 MG Oral Capsule",               "26643006", "Oral route",          500,  "mg",    3, false, "233604007", "Pneumonia"),
        ("1116635",  "Insulin glargine 100 UNT/ML Injectable Solution","34206005", "Subcutaneous route", 20,   "[iU]",  1, false, "44054006",  "Diabetes mellitus type 2"),
        ("628971",   "Morphine 2 MG/ML Injectable Solution",          "47625008", "Intravenous route",   4,    "mg",    4, true,  "57676002",  "Pain"),
        ("1860487",  "Piperacillin-tazobactam 3.375 g Injection",     "47625008", "Intravenous route",   3375, "mg",    4, false, "10001005",  "Septicemia"),
        ("1049270",  "Heparin 5000 UNT/mL Injectable Solution",       "34206005", "Subcutaneous route",  5000, "[iU]",  3, false, "59557009",  "DVT prophylaxis"),
        ("582620",   "Pantoprazole 40 MG Delayed Release Oral Tablet","26643006", "Oral route",          40,   "mg",    1, false, "34000006",  "Stress ulcer prophylaxis"),
        ("197319",   "Albuterol 0.083 MG/ML Inhalation Solution",     "6064005",  "Inhalation route",   2.5,  "mg",    4, true,  "195951007", "COPD exacerbation"),
        ("855332",   "Atorvastatin 40 MG Oral Tablet",                "26643006", "Oral route",          40,   "mg",    1, false, "414545008", "Ischemic heart disease"),
    ];

    // -----------------------------------------------------------------------
    //  Service requests
    // -----------------------------------------------------------------------

    public static readonly (string Code, string Display, bool IsLab, string System)[] ServiceRequests =
    [
        ("24331-1",   "Lipid panel - Serum or Plasma",                          true,  "http://loinc.org"),
        ("58410-2",   "CBC panel - Blood by Automated count",                   true,  "http://loinc.org"),
        ("51990-0",   "Basic metabolic panel - Blood",                          true,  "http://loinc.org"),
        ("24323-8",   "Comprehensive metabolic panel - Serum or Plasma",        true,  "http://loinc.org"),
        ("24357-6",   "Urinalysis panel",                                       true,  "http://loinc.org"),
        ("85319-2",   "Blood culture panel - Blood by Culture",                 true,  "http://loinc.org"),
        ("409073007", "Patient education",                                      false, "http://snomed.info/sct"),
        ("182744004", "Parenteral nutrition",                                   false, "http://snomed.info/sct"),
        ("306206005", "Referral to service",                                    false, "http://snomed.info/sct"),
        ("11429006",  "Consultation",                                           false, "http://snomed.info/sct"),
        ("310127009", "Physiotherapy",                                          false, "http://snomed.info/sct"),
        ("710830003", "Portable chest X-ray",                                   false, "http://snomed.info/sct"),
    ];

    // -----------------------------------------------------------------------
    //  Specimens — with container and handling
    // -----------------------------------------------------------------------

    public static readonly (
        string TypeCode, string TypeDisplay, string TypeSystem,
        string ContainerCode, string ContainerDisplay,
        string CollectionMethod, string BodySiteCode, string BodySiteDisplay)[] Specimens =
    [
        ("BLDV", "Blood venous",          "http://terminology.hl7.org/CodeSystem/v2-0488", "REDTUBE",  "Red top tube",           "Venipuncture",   "49852007", "Median cubital vein"),
        ("BLDA", "Blood arterial",        "http://terminology.hl7.org/CodeSystem/v2-0488", "GREENTUBE","Green top tube",          "Arterial stick", "17137000", "Radial artery"),
        ("UR",   "Urine",                 "http://terminology.hl7.org/CodeSystem/v2-0488", "URN",      "Urine specimen container","Catheterized",   "13648007", "Urinary bladder"),
        ("CSF",  "Cerebral spinal fluid", "http://terminology.hl7.org/CodeSystem/v2-0488", "CSFTUBE",  "CSF tube",               "Lumbar puncture","10951007", "Lumbar spinal canal"),
        ("SPT",  "Sputum",                "http://terminology.hl7.org/CodeSystem/v2-0488", "SPTCUP",   "Sputum cup",             "Expectorated",   "39607008", "Lung structure"),
        ("SWAB", "Wound swab",            "http://terminology.hl7.org/CodeSystem/v2-0488", "SWAB",     "Swab",                   "Swab",           "13648007", "Wound"),
        ("TISS", "Tissue",                "http://terminology.hl7.org/CodeSystem/v2-0488", "PATH",     "Pathology container",    "Biopsy",         "85756007", "Body tissue"),
    ];

    // -----------------------------------------------------------------------
    //  Allergies — with substance, reaction, and route of exposure
    // -----------------------------------------------------------------------

    public static readonly (
        string Code, string Display,
        string ManifestationCode, string ManifestationDisplay,
        string Severity, string ExposureRoute)[] Allergies =
    [
        ("91931000",  "Allergy to penicillin (finding)",      "271807003", "Eruption of skin",           "moderate", "26643006"),
        ("416098002", "Drug allergy to sulfonamides",         "267036007", "Dyspnoea",                   "severe",   "26643006"),
        ("414285001", "Food allergy",                         "422587007", "Nausea and vomiting",        "mild",     "26643006"),
        ("232347008", "Allergy to egg protein (finding)",     "271807003", "Eruption of skin",           "mild",     "26643006"),
        ("300917003", "Allergy to latex",                     "29857009",  "Chest pain",                 "moderate", "6056007"),
        ("419199007", "Allergy to substance",                 "25064002",  "Headache",                   "mild",     "26643006"),
        ("419511003", "Allergy to aspirin",                   "267036007", "Dyspnoea",                   "severe",   "26643006"),
        ("294505008", "Allergy to cephalosporin",             "271807003", "Eruption of skin",           "moderate", "26643006"),
    ];

    // -----------------------------------------------------------------------
    //  Immunizations
    // -----------------------------------------------------------------------

    public static readonly (string CvxCode, string Display, double DoseML)[] Immunizations =
    [
        ("140", "Influenza, seasonal, injectable, preservative free", 0.5),
        ("113", "Td (adult) preservative free",                       0.5),
        ("33",  "Pneumococcal polysaccharide PPV23",                  0.5),
        ("115", "Tdap",                                               0.5),
        ("83",  "Hepatitis A, pediatric/adolescent, 2 dose schedule", 1.0),
        ("45",  "Hepatitis B, adult",                                  1.0),
        ("20",  "DTaP",                                               0.5),
        ("10",  "IPV",                                                0.5),
        ("119", "Rotavirus, monovalent",                              1.0),
        ("207", "COVID-19, mRNA, LNP-S, PF, 100 mcg/0.5mL dose",    0.5),
    ];

    // -----------------------------------------------------------------------
    //  Imaging studies — with interpreter and reason
    // -----------------------------------------------------------------------

    public static readonly (
        string SnomedCode, string Display, string Modality,
        string BodySiteCode, string BodySiteDisplay,
        string ReasonCode, string ReasonDisplay)[] ImagingStudies =
    [
        ("433236007", "Transthoracic echocardiography",    "US", "80891009", "Heart structure",     "84114007",  "Heart failure"),
        ("399208008", "Plain chest X-ray",                 "DX", "51185008", "Thoracic structure",  "233604007", "Pneumonia"),
        ("77477000",  "CT of head",                        "CT", "69536005", "Head structure",      "422504002", "Ischaemic stroke"),
        ("113091000", "MRI of head",                       "MR", "69536005", "Head structure",      "422504002", "Ischaemic stroke"),
        ("44179004",  "Fluoroscopy of chest",              "RF", "39607008", "Lung structure",      "233604007", "Pneumonia"),
        ("42146005",  "Ultrasound of abdomen",             "US", "818983003","Abdomen",             "14669001",  "Acute renal failure"),
        ("80966001",  "CT of pelvis",                      "CT", "816092008","Pelvis",              "700097003", "Hip fracture"),
    ];

    // -----------------------------------------------------------------------
    //  Diagnostic report panels — all genuine lab/radiology panels.
    //  category uses v2-0074 codes only (HM, CH, UA, MB, RAD, PT).
    //  Clinical notes (H&P, discharge summary) live in DocumentTypes only.
    // -----------------------------------------------------------------------

    public static readonly (string Code, string Display, string CategoryCode, string CategoryDisplay)[] DiagnosticReports =
    [
        ("58410-2", "CBC panel - Blood by Automated count",              "HM",  "Hematology"),
        ("24323-8", "Comprehensive metabolic panel - Serum or Plasma",   "CH",  "Chemistry"),
        ("24331-1", "Lipid panel - Serum or Plasma",                     "CH",  "Chemistry"),
        ("57698-3", "Lipid panel with direct LDL - Serum or Plasma",     "CH",  "Chemistry"),
        ("24357-6", "Urinalysis panel - Urine",                          "CH",  "Chemistry"),
        ("85319-2", "Blood culture panel",                               "MB",  "Microbiology"),
        ("24627-2", "Chest X-ray AP",                                    "RAD", "Radiology"),
        ("30954-2", "Relevant diagnostic tests/laboratory data Narrative","CH",  "Chemistry"),
    ];

    // -----------------------------------------------------------------------
    //  Document types (for DocumentReference)
    // -----------------------------------------------------------------------

    public static readonly (string Code, string Display, string ClassCode, string ClassDisplay)[] DocumentTypes =
    [
        ("11488-4", "Consult note",            "LP173057-5", "Consult note"),
        ("18842-5", "Discharge summary",       "LP173421-1", "Discharge summary"),
        ("34117-2", "History and physical note","LP29684-5", "History and physical note"),
        ("11506-3", "Progress note",           "LP173418-7", "Progress note"),
        ("57133-1", "Referral note",           "LP173420-3", "Referral note"),
        ("34749-9", "Anesthesiology note",     "LP173057-5", "Procedure note"),
        ("28651-8", "Nurse progress note",     "LP173418-7", "Progress note"),
    ];

    // -----------------------------------------------------------------------
    //  Care plan activities
    // -----------------------------------------------------------------------

    public static readonly (string Code, string Display, string GoalCode, string GoalDisplay)[] CarePlanActivities =
    [
        ("229070002", "Dietary education",                "160670007", "Adherent to diet"),
        ("229070002", "Dietary education",                "160670007", "Adherent to diet"),
        ("229124009", "Fluid management",                 "405773007", "Adequate fluid intake"),
        ("229070002", "Dietary sodium restriction education", "413398009", "Reduced sodium diet"),
        ("103735009", "Palliative care",                  "182970005", "Comfortable"),
        ("40701008",  "Echocardiography follow-up",       "405773007", "Normal cardiac function"),
        ("182804009", "Drug compliance therapy",          "182992009", "Medication adherence"),
        ("229772003", "Ambulation therapy",               "160676001", "Walking independently"),
        ("229070002", "Respiratory therapy education",    "405773007", "Adequate oxygenation"),
        ("385949008", "Wound care",                       "406203001", "Wound healed"),
    ];
}
