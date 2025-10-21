namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Maps each <see cref="FhirGenerationCodes.ClinicalScenarios"/> index to the
/// pool indices that are clinically appropriate for that admission story.
///
/// When generating resources for a patient, the resource loop picks from
/// these subsets instead of the entire global pool so that a pneumonia
/// patient gets antibiotics and chest X-rays, not insulin and echocardiograms.
///
/// Indices that appear in every scenario (e.g. acetaminophen, DVT prophylaxis,
/// CBC, BMP, vitals) represent universal inpatient care and are included as
/// <see cref="UniversalMedicationIndices"/>, <see cref="UniversalObservationIndices"/>, etc.
/// </summary>
public static class ScenarioResourceMap
{
    // -----------------------------------------------------------------------
    //  Universal inpatient resources — included for EVERY scenario
    // -----------------------------------------------------------------------

    /// <summary>Medications given to virtually every inpatient (fever PRN, DVT prophylaxis, PPI, etc.).</summary>
    public static readonly int[] UniversalMedicationIndices = [0, 2, 11, 12]; // Acetaminophen, Enoxaparin, Heparin, Pantoprazole

    /// <summary>Observations performed on every inpatient (vitals + basic labs + social-history + survey).</summary>
    public static readonly int[] UniversalObservationIndices =
    [
        0, 1, 2, 5, 6, 7,      // HR, Temp, SpO2, BP, RR, BMI
        8, 9, 10, 11,           // Hgb, Hct, WBC, Plt
        12, 13, 14, 15, 16,     // Cr, Glucose, Na, K, Cl
        38, 39                  // Social-history (smoking status), Survey (PHQ-9)
    ];

    /// <summary>Specimen types used universally (venous blood, urine).</summary>
    public static readonly int[] UniversalSpecimenIndices = [0, 2]; // Blood venous, Urine

    /// <summary>Service requests ordered universally (CBC, BMP).</summary>
    public static readonly int[] UniversalServiceRequestIndices = [1, 2]; // CBC, BMP

    /// <summary>Comorbidities that can appear on any patient (HTN, DM2, health concern).</summary>
    public static readonly int[] UniversalConditionIndices = [0, 1, 24]; // DM2, HTN, Health concern

    // -----------------------------------------------------------------------
    //  Per-scenario resource mappings
    //  Index into FhirGenerationCodes arrays (0-based)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Scenario-specific medication indices.
    /// These are ADDED to <see cref="UniversalMedicationIndices"/> during generation.
    /// </summary>
    public static readonly int[][] ScenarioMedicationIndices =
    [
        // 0 — Pneumonia: ceftriaxone, amoxicillin
        [1, 7],
        // 1 — Heart failure: metoprolol, furosemide, lisinopril
        [4, 5, 6],
        // 2 — Acute MI: metoprolol, atorvastatin, morphine, heparin
        [4, 9, 11, 14],
        // 3 — COPD exacerbation: albuterol (+ universal DVT prophylaxis)
        [13],
        // 4 — Sepsis: vancomycin, piperacillin-tazobactam, ceftriaxone
        [1, 3, 10],
        // 5 — Hip fracture: morphine (pain), heparin (DVT prophylaxis)
        [9, 11],
        // 6 — Acute renal failure: furosemide, lisinopril
        [5, 6],
        // 7 — Ischaemic stroke: atorvastatin, heparin
        [11, 14],
        // 8 — DKA: insulin regular IV, potassium replacement, normal saline
        [8, 15, 16, 23],
        // 9 — GI bleed: omeprazole, morphine (pain)
        [9, 17],
        // 10 — Pulmonary embolism: heparin, warfarin
        [11, 18],
        // 11 — Acute pancreatitis: morphine (pain), normal saline
        [9, 23],
        // 12 — Cellulitis: vancomycin, metronidazole
        [3, 22],
        // 13 — AFib with RVR: diltiazem, metoprolol, atorvastatin
        [4, 14, 19],
        // 14 — Diabetic hypoglycemia: dextrose 50%, glucagon, insulin glargine (held/adjusted)
        [8, 20, 24],
        // 15 — Acute appendicitis: cefazolin, metronidazole, morphine
        [9, 21, 22],
    ];

    /// <summary>
    /// Scenario-specific procedure indices.
    /// </summary>
    public static readonly int[][] ScenarioProcedureIndices =
    [
        // 0 — Pneumonia: thoracentesis
        [8],
        // 1 — Heart failure: echocardiography, heart valve replacement
        [6, 12],
        // 2 — Acute MI: CABG, aortic aneurysm repair
        [5, 10],
        // 3 — COPD exacerbation: artificial respiration, endotracheal intubation
        [2, 11],
        // 4 — Sepsis: urinary catheterization, PICC insertion
        [0, 4],
        // 5 — Hip fracture: wound care management
        [1],
        // 6 — Acute renal failure: dialysis, renal dialysis
        [3, 7],
        // 7 — Ischaemic stroke: bone marrow biopsy (diagnostic workup)
        [9],
        // 8 — DKA: insulin infusion protocol, PICC insertion
        [4, 19],
        // 9 — GI bleed: EGD (upper endoscopy), blood transfusion
        [13, 18],
        // 10 — Pulmonary embolism: CT pulmonary angiography procedure
        [14],
        // 11 — Acute pancreatitis: PICC insertion (TPN access)
        [4],
        // 12 — Cellulitis: incision and drainage, wound care
        [1, 17],
        // 13 — AFib with RVR: cardioversion, echocardiography
        [12, 16],
        // 14 — Diabetic hypoglycemia: (supportive care, no major procedures)
        [],
        // 15 — Acute appendicitis: appendectomy
        [15],
    ];

    /// <summary>
    /// Scenario-specific observation indices (beyond universal vitals/labs).
    /// </summary>
    public static readonly int[][] ScenarioObservationIndices =
    [
        // 0 — Pneumonia: WBC elevated, LDH, blood culture
        [10, 23, 24],
        // 1 — Heart failure: NT-proBNP, troponin, cholesterol panel
        [20, 21, 27, 28],
        // 2 — Acute MI: troponin, NT-proBNP, PT/INR
        [25, 26, 27, 28],
        // 3 — COPD exacerbation: SpO2, respiratory rate
        [2, 6],
        // 4 — Sepsis: WBC, blood culture, bilirubin, lactate
        [10, 17, 23, 24, 34],
        // 5 — Hip fracture: hemoglobin (blood loss), PT/INR (pre-op)
        [8, 25, 26],
        // 6 — Acute renal failure: creatinine, GFR, potassium, sodium
        [12, 15, 22],
        // 7 — Ischaemic stroke: glucose, cholesterol, PT/INR
        [13, 20, 25, 26],
        // 8 — DKA: blood glucose, POC glucose, base excess, potassium, calcium, lactate
        [13, 29, 30, 31, 34, 37],
        // 9 — GI bleed: hemoglobin, hematocrit, PT/INR, albumin
        [8, 9, 25, 26, 36],
        // 10 — Pulmonary embolism: troponin, NT-proBNP, PT/INR, D-dimer proxy (LDH)
        [23, 25, 26, 27, 28],
        // 11 — Acute pancreatitis: lipase, bilirubin, calcium, CRP, ALT
        [17, 18, 31, 32, 33],
        // 12 — Cellulitis: WBC, CRP, blood culture
        [10, 24, 32],
        // 13 — AFib with RVR: troponin, NT-proBNP, potassium, magnesium proxy (calcium)
        [15, 27, 28, 31],
        // 14 — Diabetic hypoglycemia: blood glucose (critically low), POC glucose, potassium, base excess
        [13, 15, 29, 30, 37],
        // 15 — Acute appendicitis: WBC, CRP, bilirubin
        [10, 17, 32],
    ];

    /// <summary>
    /// Scenario-specific specimen type indices (beyond universal blood/urine).
    /// </summary>
    public static readonly int[][] ScenarioSpecimenIndices =
    [
        // 0 — Pneumonia: sputum
        [4],
        // 1 — Heart failure: arterial blood
        [1],
        // 2 — Acute MI: arterial blood
        [1],
        // 3 — COPD exacerbation: sputum, arterial blood
        [1, 4],
        // 4 — Sepsis: wound swab, arterial blood
        [1, 5],
        // 5 — Hip fracture: tissue (surgical)
        [6],
        // 6 — Acute renal failure: (universal only)
        [],
        // 7 — Ischaemic stroke: CSF (lumbar puncture workup)
        [3],
        // 8 — DKA: arterial blood (ABG)
        [1],
        // 9 — GI bleed: (universal blood + urine)
        [],
        // 10 — Pulmonary embolism: arterial blood
        [1],
        // 11 — Acute pancreatitis: (universal blood + urine)
        [],
        // 12 — Cellulitis: wound swab
        [5],
        // 13 — AFib with RVR: arterial blood
        [1],
        // 14 — Diabetic hypoglycemia: arterial blood (ABG)
        [1],
        // 15 — Acute appendicitis: tissue (surgical pathology)
        [6],
    ];

    /// <summary>
    /// Scenario-specific imaging study indices.
    /// </summary>
    public static readonly int[][] ScenarioImagingIndices =
    [
        // 0 — Pneumonia: chest X-ray, fluoroscopy of chest
        [1, 4],
        // 1 — Heart failure: echocardiography
        [0],
        // 2 — Acute MI: echocardiography
        [0],
        // 3 — COPD exacerbation: chest X-ray
        [1],
        // 4 — Sepsis: ultrasound of abdomen
        [5],
        // 5 — Hip fracture: CT pelvis
        [6],
        // 6 — Acute renal failure: ultrasound of abdomen
        [5],
        // 7 — Ischaemic stroke: CT head, MRI head
        [2, 3],
        // 8 — DKA: chest X-ray (rule out infection trigger)
        [1],
        // 9 — GI bleed: CT abdomen/pelvis
        [7],
        // 10 — Pulmonary embolism: CT pulmonary angiography, chest X-ray
        [1, 8],
        // 11 — Acute pancreatitis: CT abdomen/pelvis, ultrasound of abdomen
        [5, 7],
        // 12 — Cellulitis: ultrasound of lower extremity
        [9],
        // 13 — AFib with RVR: echocardiography, chest X-ray
        [0, 1],
        // 14 — Diabetic hypoglycemia: CT head (rule out stroke)
        [2],
        // 15 — Acute appendicitis: CT abdomen/pelvis, abdominal X-ray
        [7, 10],
    ];

    /// <summary>
    /// Scenario-specific service request indices (beyond universal CBC/BMP).
    /// </summary>
    public static readonly int[][] ScenarioServiceRequestIndices =
    [
        // 0 — Pneumonia: blood culture panel, portable chest X-ray
        [5, 11],
        // 1 — Heart failure: lipid panel, consultation, physiotherapy
        [0, 9, 10],
        // 2 — Acute MI: lipid panel, consultation
        [0, 9],
        // 3 — COPD exacerbation: patient education, physiotherapy
        [6, 10],
        // 4 — Sepsis: blood culture, urinalysis, referral
        [4, 5, 8],
        // 5 — Hip fracture: physiotherapy, referral
        [8, 10],
        // 6 — Acute renal failure: CMP, consultation, parenteral nutrition
        [3, 7, 9],
        // 7 — Ischaemic stroke: consultation, physiotherapy, referral
        [8, 9, 10],
        // 8 — DKA: CMP, endocrinology consult
        [3, 15],
        // 9 — GI bleed: blood type/crossmatch, hepatic function panel, consultation
        [9, 13, 14],
        // 10 — Pulmonary embolism: coagulation panel, chest X-ray
        [11, 12],
        // 11 — Acute pancreatitis: CMP, hepatic function panel, parenteral nutrition
        [3, 7, 13],
        // 12 — Cellulitis: blood culture, consultation
        [5, 9],
        // 13 — AFib with RVR: lipid panel, coagulation panel, consultation
        [0, 9, 12],
        // 14 — Diabetic hypoglycemia: CMP, endocrinology consult
        [3, 15],
        // 15 — Acute appendicitis: blood type/crossmatch, surgical consult
        [14, 16],
    ];

    /// <summary>
    /// Scenario-specific comorbidity condition indices (beyond universal HTN/DM2).
    /// </summary>
    public static readonly int[][] ScenarioConditionIndices =
    [
        // 0 — Pneumonia: fever, nausea
        [11, 13],
        // 1 — Heart failure: AFib, ischemic heart disease
        [4, 6],
        // 2 — Acute MI: ischemic heart disease, AFib
        [4, 6],
        // 3 — COPD exacerbation: COPD (comorbid), stress
        [2, 15],
        // 4 — Sepsis: fever, AKI
        [7, 11],
        // 5 — Hip fracture: joint pain, nausea
        [13, 14],
        // 6 — Acute renal failure: HTN (already universal), DM
        [3],
        // 7 — Ischaemic stroke: CVA, headache, AFib
        [6, 9, 12],
        // 8 — DKA: DKA dx, dehydration, DM2
        [0, 16, 17],
        // 9 — GI bleed: GI hemorrhage, nausea
        [13, 18],
        // 10 — Pulmonary embolism: DVT, AFib
        [6, 20],
        // 11 — Acute pancreatitis: gallstones, nausea
        [13, 21],
        // 12 — Cellulitis: fever
        [11],
        // 13 — AFib with RVR: ischemic heart disease, HTN (already universal)
        [4, 19],
        // 14 — Diabetic hypoglycemia: hypoglycemia, DM2
        [0, 22],
        // 15 — Acute appendicitis: fever, nausea
        [11, 13],
    ];

    /// <summary>
    /// Build the merged index array for a resource pool, combining universal + scenario-specific.
    /// </summary>
    public static int[] GetMergedIndices(int[] universalIndices, int[][] scenarioIndices, int scenarioIndex, int poolLength)
    {
        var safeScenario = ((scenarioIndex % scenarioIndices.Length) + scenarioIndices.Length) % scenarioIndices.Length;
        var merged = new HashSet<int>(universalIndices);
        foreach (var idx in scenarioIndices[safeScenario])
        {
            if (idx < poolLength)
                merged.Add(idx);
        }
        var result = merged.ToArray();
        Array.Sort(result);
        return result;
    }

    /// <summary>
    /// Pick from a scenario-appropriate subset of the pool using a seed.
    /// Falls back to full pool modulo if the subset is empty.
    /// </summary>
    public static int PickIndex(int[] allowedIndices, int seed, int poolLength)
    {
        if (allowedIndices.Length == 0)
            return ((seed % poolLength) + poolLength) % poolLength;
        return allowedIndices[((seed % allowedIndices.Length) + allowedIndices.Length) % allowedIndices.Length];
    }
}
