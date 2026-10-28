db = connect( 'mongodb://localhost/link-measureeval' );

db.measureDefinition.insertOne(
{
	"_id": "NHSNdQMAcuteCareHospitalInitialPopulation",
	"bundle": {
	"resourceType": "Bundle",
	"id": "NHSNdQMAcuteCareHospitalInitialPopulation-bundle",
	"type": "transaction",
	"timestamp": "2024-04-16T10:34:54.881-07:00",
	"entry": [
	  {
		"resource": {
		  "resourceType": "ValueSet",
		  "id": "2.16.840.1.114222.4.11.837",
		  "meta": {
			"versionId": "1",
			"lastUpdated": "2012-10-25T12:28:31.000-04:00"
		  },
		  "text": {
			"status": "generated",
			"div": "<div xmlns=\"http://www.w3.org/1999/xhtml\">\n         <h3>Value Set Contents</h3>\n         <p>This value set contains 2 concepts</p>\n         <p>All codes from system \n            <code>urn:oid:2.16.840.1.113883.6.238</code>\n         </p>\n         <table class=\"codes\">\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <b>Code</b>\n               </td>\n               <td>\n                  <b>Display</b>\n               </td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"urn-oid-2.16.840.1.113883.6.238-2135-2\"> </a>2135-2\n               </td>\n               <td>Hispanic or Latino</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"urn-oid-2.16.840.1.113883.6.238-2186-5\"> </a>2186-5\n               </td>\n               <td>Not Hispanic or Latino</td>\n            </tr>\n         </table>\n      </div>"
		  },
		  "url": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.114222.4.11.837",
		  "version": "20121025",
		  "name": "Ethnicity",
		  "title": "Ethnicity",
		  "status": "active",
		  "date": "2012-10-25T12:28:31-04:00",
		  "publisher": "CDC NCHS",
		  "description": "The purpose of this value set is to represent CDC concepts for Ethnicity",
		  "expansion": {
			"identifier": "urn:uuid:4ee1b2a1-fb24-4145-afaa-30345e5e0ea2",
			"timestamp": "2022-03-09T10:12:54-05:00",
			"total": 2,
			"offset": 0,
			"parameter": [
			  {
				"name": "count",
				"valueInteger": 1000
			  },
			  {
				"name": "offset",
				"valueInteger": 0
			  }
			],
			"contains": [
			  {
				"system": "urn:oid:2.16.840.1.113883.6.238",
				"version": "1.2",
				"code": "2135-2",
				"display": "Hispanic or Latino"
			  },
			  {
				"system": "urn:oid:2.16.840.1.113883.6.238",
				"version": "1.2",
				"code": "2186-5",
				"display": "Not Hispanic or Latino"
			  }
			]
		  }
		},
		"request": {
		  "method": "PUT",
		  "url": "ValueSet/2.16.840.1.114222.4.11.837/_history/1"
		}
	  },
	  {
		"resource": {
		  "resourceType": "ValueSet",
		  "id": "2.16.840.1.114222.4.11.3591",
		  "meta": {
			"versionId": "14",
			"lastUpdated": "2018-07-18T01:09:05.000-04:00"
		  },
		  "text": {
			"status": "generated",
			"div": "<div xmlns=\"http://www.w3.org/1999/xhtml\">\n         <h3>Value Set Contents</h3>\n         <p>This value set contains 162 concepts</p>\n         <p>All codes from system \n            <code>https://nahdo.org/sopt</code>\n         </p>\n         <table class=\"codes\">\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <b>Code</b>\n               </td>\n               <td>\n                  <b>Display</b>\n               </td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-1\"> </a>1\n               </td>\n               <td>MEDICARE</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-11\"> </a>11\n               </td>\n               <td>Medicare Managed Care (Includes Medicare Advantage Plans)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-111\"> </a>111\n               </td>\n               <td>Medicare HMO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-112\"> </a>112\n               </td>\n               <td>Medicare PPO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-113\"> </a>113\n               </td>\n               <td>Medicare POS</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-119\"> </a>119\n               </td>\n               <td>Medicare Managed Care Other</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-12\"> </a>12\n               </td>\n               <td>Medicare (Non-managed Care)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-121\"> </a>121\n               </td>\n               <td>Medicare FFS</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-122\"> </a>122\n               </td>\n               <td>Medicare Drug Benefit</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-123\"> </a>123\n               </td>\n               <td>Medicare Medical Savings Account (MSA)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-129\"> </a>129\n               </td>\n               <td>Medicare Non-managed Care Other</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-13\"> </a>13\n               </td>\n               <td>Medicare Hospice</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-14\"> </a>14\n               </td>\n               <td>Dual Eligibility Medicare/Medicaid Organization</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-19\"> </a>19\n               </td>\n               <td>Medicare Other</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-191\"> </a>191\n               </td>\n               <td>Medicare Pharmacy Benefit Manager</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-2\"> </a>2\n               </td>\n               <td>MEDICAID</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-21\"> </a>21\n               </td>\n               <td>Medicaid (Managed Care)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-211\"> </a>211\n               </td>\n               <td>Medicaid HMO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-212\"> </a>212\n               </td>\n               <td>Medicaid PPO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-213\"> </a>213\n               </td>\n               <td>Medicaid PCCM (Primary Care Case Management)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-219\"> </a>219\n               </td>\n               <td>Medicaid Managed Care Other</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-22\"> </a>22\n               </td>\n               <td>Medicaid (Non-managed Care Plan)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-23\"> </a>23\n               </td>\n               <td>Medicaid/SCHIP</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-25\"> </a>25\n               </td>\n               <td>Medicaid - Out of State</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-26\"> </a>26\n               </td>\n               <td>Medicaid - Long Term Care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-29\"> </a>29\n               </td>\n               <td>Medicaid Other</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-291\"> </a>291\n               </td>\n               <td>Medicaid Pharmacy Benefit Manager</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-299\"> </a>299\n               </td>\n               <td>Medicaid - Dental</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3\"> </a>3\n               </td>\n               <td>OTHER GOVERNMENT (Federal/State/Local) (excluding Department of Corrections)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-31\"> </a>31\n               </td>\n               <td>Department of Defense</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-311\"> </a>311\n               </td>\n               <td>TRICARE (CHAMPUS)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3111\"> </a>3111\n               </td>\n               <td>TRICARE Prime--HMO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3112\"> </a>3112\n               </td>\n               <td>TRICARE Extra--PPO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3113\"> </a>3113\n               </td>\n               <td>TRICARE Standard - Fee For Service</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3114\"> </a>3114\n               </td>\n               <td>TRICARE For Life--Medicare Supplement</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3115\"> </a>3115\n               </td>\n               <td>TRICARE Reserve Select</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3116\"> </a>3116\n               </td>\n               <td>Uniformed Services Family Health Plan (USFHP) -- HMO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3119\"> </a>3119\n               </td>\n               <td>Department of Defense - (other)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-312\"> </a>312\n               </td>\n               <td>Military Treatment Facility</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3121\"> </a>3121\n               </td>\n               <td>Enrolled Prime--HMO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3122\"> </a>3122\n               </td>\n               <td>Non-enrolled Space Available</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3123\"> </a>3123\n               </td>\n               <td>TRICARE For Life (TFL)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-313\"> </a>313\n               </td>\n               <td>Dental --Stand Alone</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-32\"> </a>32\n               </td>\n               <td>Department of Veterans Affairs</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-321\"> </a>321\n               </td>\n               <td>Veteran care-Care provided to Veterans</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3211\"> </a>3211\n               </td>\n               <td>Direct Care-Care provided in VA facilities</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3212\"> </a>3212\n               </td>\n               <td>Indirect Care-Care provided outside VA facilities</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-32121\"> </a>32121\n               </td>\n               <td>Fee Basis</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-32122\"> </a>32122\n               </td>\n               <td>Foreign Fee/Foreign Medical Program (FMP)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-32123\"> </a>32123\n               </td>\n               <td>Contract Nursing Home/Community Nursing Home</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-32124\"> </a>32124\n               </td>\n               <td>State Veterans Home</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-32125\"> </a>32125\n               </td>\n               <td>Sharing Agreements</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-32126\"> </a>32126\n               </td>\n               <td>Other Federal Agency</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-32127\"> </a>32127\n               </td>\n               <td>Dental Care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-32128\"> </a>32128\n               </td>\n               <td>Vision Care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-322\"> </a>322\n               </td>\n               <td>Non-veteran care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3221\"> </a>3221\n               </td>\n               <td>Civilian Health and Medical Program for the VA (CHAMPVA)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3222\"> </a>3222\n               </td>\n               <td>Spina Bifida Health Care Program (SB)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3223\"> </a>3223\n               </td>\n               <td>Children of Women Vietnam Veterans (CWVV)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3229\"> </a>3229\n               </td>\n               <td>Other non-veteran care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-33\"> </a>33\n               </td>\n               <td>Indian Health Service or Tribe</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-331\"> </a>331\n               </td>\n               <td>Indian Health Service - Regular</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-332\"> </a>332\n               </td>\n               <td>Indian Health Service - Contract</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-333\"> </a>333\n               </td>\n               <td>Indian Health Service - Managed Care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-334\"> </a>334\n               </td>\n               <td>Indian Tribe - Sponsored Coverage</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-34\"> </a>34\n               </td>\n               <td>HRSA Program</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-341\"> </a>341\n               </td>\n               <td>Title V (MCH Block Grant)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-342\"> </a>342\n               </td>\n               <td>Migrant Health Program</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-343\"> </a>343\n               </td>\n               <td>Ryan White Act</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-349\"> </a>349\n               </td>\n               <td>Other</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-35\"> </a>35\n               </td>\n               <td>Black Lung</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-36\"> </a>36\n               </td>\n               <td>State Government</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-361\"> </a>361\n               </td>\n               <td>State SCHIP program (codes for individual states)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-362\"> </a>362\n               </td>\n               <td>Specific state programs (list/ local code)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-369\"> </a>369\n               </td>\n               <td>State, not otherwise specified (other state)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-37\"> </a>37\n               </td>\n               <td>Local Government</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-371\"> </a>371\n               </td>\n               <td>Local - Managed care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3711\"> </a>3711\n               </td>\n               <td>HMO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3712\"> </a>3712\n               </td>\n               <td>PPO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3713\"> </a>3713\n               </td>\n               <td>POS</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-372\"> </a>372\n               </td>\n               <td>FFS/Indemnity</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-379\"> </a>379\n               </td>\n               <td>Local, not otherwise specified (other local, county)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-38\"> </a>38\n               </td>\n               <td>Other Government (Federal, State, Local not specified)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-381\"> </a>381\n               </td>\n               <td>Federal, State, Local not specified managed care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3811\"> </a>3811\n               </td>\n               <td>Federal, State, Local not specified - HMO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3812\"> </a>3812\n               </td>\n               <td>Federal, State, Local not specified - PPO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3813\"> </a>3813\n               </td>\n               <td>Federal, State, Local not specified - POS</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-3819\"> </a>3819\n               </td>\n               <td>Federal, State, Local not specified - not specified managed care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-382\"> </a>382\n               </td>\n               <td>Federal, State, Local not specified - FFS</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-389\"> </a>389\n               </td>\n               <td>Federal, State, Local not specified - Other</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-39\"> </a>39\n               </td>\n               <td>Other Federal</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-391\"> </a>391\n               </td>\n               <td>Federal Employee Health Plan - Use when known</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-4\"> </a>4\n               </td>\n               <td>DEPARTMENTS OF CORRECTIONS</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-41\"> </a>41\n               </td>\n               <td>Corrections Federal</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-42\"> </a>42\n               </td>\n               <td>Corrections State</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-43\"> </a>43\n               </td>\n               <td>Corrections Local</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-44\"> </a>44\n               </td>\n               <td>Corrections Unknown Level</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-5\"> </a>5\n               </td>\n               <td>PRIVATE HEALTH INSURANCE</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-51\"> </a>51\n               </td>\n               <td>Managed Care (Private)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-511\"> </a>511\n               </td>\n               <td>Commercial Managed Care - HMO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-512\"> </a>512\n               </td>\n               <td>Commercial Managed Care - PPO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-513\"> </a>513\n               </td>\n               <td>Commercial Managed Care - POS</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-514\"> </a>514\n               </td>\n               <td>Exclusive Provider Organization</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-515\"> </a>515\n               </td>\n               <td>Gatekeeper PPO (GPPO)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-516\"> </a>516\n               </td>\n               <td>Commercial Managed Care - Pharmacy Benefit Manager</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-517\"> </a>517\n               </td>\n               <td>Commercial Managed Care - Dental</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-519\"> </a>519\n               </td>\n               <td>Managed Care, Other (non HMO)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-52\"> </a>52\n               </td>\n               <td>Private Health Insurance - Indemnity</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-521\"> </a>521\n               </td>\n               <td>Commercial Indemnity</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-522\"> </a>522\n               </td>\n               <td>Self-insured (ERISA) Administrative Services Only (ASO) plan</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-523\"> </a>523\n               </td>\n               <td>Medicare supplemental policy (as second payer)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-524\"> </a>524\n               </td>\n               <td>Indemnity Insurance - Dental</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-529\"> </a>529\n               </td>\n               <td>Private health insurance--other commercial Indemnity</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-53\"> </a>53\n               </td>\n               <td>Managed Care (private) or private health insurance (indemnity), not otherwise specified</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-54\"> </a>54\n               </td>\n               <td>Organized Delivery System</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-55\"> </a>55\n               </td>\n               <td>Small Employer Purchasing Group</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-56\"> </a>56\n               </td>\n               <td>Specialized Stand-Alone Plan</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-561\"> </a>561\n               </td>\n               <td>Dental</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-562\"> </a>562\n               </td>\n               <td>Vision</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-59\"> </a>59\n               </td>\n               <td>Other Private Insurance</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-6\"> </a>6\n               </td>\n               <td>BLUE CROSS/BLUE SHIELD</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-61\"> </a>61\n               </td>\n               <td>BC Managed Care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-611\"> </a>611\n               </td>\n               <td>BC Managed Care - HMO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-612\"> </a>612\n               </td>\n               <td>BC Managed Care - PPO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-613\"> </a>613\n               </td>\n               <td>BC Managed Care - POS</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-614\"> </a>614\n               </td>\n               <td>BC Managed Care - Dental</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-619\"> </a>619\n               </td>\n               <td>BC Managed Care - Other</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-62\"> </a>62\n               </td>\n               <td>BC Insurance Indemnity</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-621\"> </a>621\n               </td>\n               <td>BC Indemnity</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-622\"> </a>622\n               </td>\n               <td>BC Self-insured (ERISA) Administrative Services Only (ASO)Plan</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-623\"> </a>623\n               </td>\n               <td>BC Medicare Supplemental Plan</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-629\"> </a>629\n               </td>\n               <td>BC Indemnity - Dental</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-7\"> </a>7\n               </td>\n               <td>MANAGED CARE, UNSPECIFIED (to be used only if one can't distinguish public from private)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-71\"> </a>71\n               </td>\n               <td>HMO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-72\"> </a>72\n               </td>\n               <td>PPO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-73\"> </a>73\n               </td>\n               <td>POS</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-79\"> </a>79\n               </td>\n               <td>Other Managed Care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-8\"> </a>8\n               </td>\n               <td>NO PAYMENT from an Organization/Agency/Program/Private Payer Listed</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-81\"> </a>81\n               </td>\n               <td>Self-pay (Includes applicants for insurance and Medicaid applicants)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-82\"> </a>82\n               </td>\n               <td>No Charge</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-821\"> </a>821\n               </td>\n               <td>Charity</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-822\"> </a>822\n               </td>\n               <td>Professional Courtesy</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-823\"> </a>823\n               </td>\n               <td>Research/Clinical Trial</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-83\"> </a>83\n               </td>\n               <td>Refusal to Pay/Bad Debt</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-84\"> </a>84\n               </td>\n               <td>Hill Burton Free Care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-85\"> </a>85\n               </td>\n               <td>Research/Donor</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-89\"> </a>89\n               </td>\n               <td>No Payment, Other</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-9\"> </a>9\n               </td>\n               <td>MISCELLANEOUS/OTHER</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-91\"> </a>91\n               </td>\n               <td>Foreign National</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-92\"> </a>92\n               </td>\n               <td>Other (Non-government)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-93\"> </a>93\n               </td>\n               <td>Disability Insurance</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-94\"> </a>94\n               </td>\n               <td>Long-term Care Insurance</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-95\"> </a>95\n               </td>\n               <td>Worker's Compensation</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-951\"> </a>951\n               </td>\n               <td>Worker's Comp HMO</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-953\"> </a>953\n               </td>\n               <td>Worker's Comp Fee-for-Service</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-954\"> </a>954\n               </td>\n               <td>Worker's Comp Other Managed Care</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-959\"> </a>959\n               </td>\n               <td>Worker's Comp, Other unspecified</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-96\"> </a>96\n               </td>\n               <td>Auto Insurance (includes no fault)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-97\"> </a>97\n               </td>\n               <td>Legal Liability / Liability Insurance</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-98\"> </a>98\n               </td>\n               <td>Other specified but not otherwise classifiable (includes Hospice - Unspecified plan)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-99\"> </a>99\n               </td>\n               <td>No Typology Code available for payment source</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"https---nahdo.org-sopt-9999\"> </a>9999\n               </td>\n               <td>Unavailable / No Payer Specified / Blank</td>\n            </tr>\n         </table>\n      </div>"
		  },
		  "url": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.114222.4.11.3591",
		  "version": "20180718",
		  "name": "Payer",
		  "title": "Payer",
		  "status": "active",
		  "date": "2018-07-18T01:00:04-04:00",
		  "publisher": "HL7 Terminology",
		  "description": "Categories of types of health care payor entities as defined by the US Public Health Data Consortium SOP code system",
		  "expansion": {
			"identifier": "urn:uuid:a5a88a41-73c9-45a6-9d07-7e887f7fa830",
			"timestamp": "2022-03-09T10:13:23-05:00",
			"total": 162,
			"offset": 0,
			"parameter": [
			  {
				"name": "count",
				"valueInteger": 1000
			  },
			  {
				"name": "offset",
				"valueInteger": 0
			  }
			],
			"contains": [
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "1",
				"display": "MEDICARE"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "11",
				"display": "Medicare Managed Care (Includes Medicare Advantage Plans)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "111",
				"display": "Medicare HMO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "112",
				"display": "Medicare PPO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "113",
				"display": "Medicare POS"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "119",
				"display": "Medicare Managed Care Other"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "12",
				"display": "Medicare (Non-managed Care)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "121",
				"display": "Medicare FFS"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "122",
				"display": "Medicare Drug Benefit"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "123",
				"display": "Medicare Medical Savings Account (MSA)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "129",
				"display": "Medicare Non-managed Care Other"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "13",
				"display": "Medicare Hospice"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "14",
				"display": "Dual Eligibility Medicare/Medicaid Organization"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "19",
				"display": "Medicare Other"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "191",
				"display": "Medicare Pharmacy Benefit Manager"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "2",
				"display": "MEDICAID"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "21",
				"display": "Medicaid (Managed Care)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "211",
				"display": "Medicaid HMO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "212",
				"display": "Medicaid PPO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "213",
				"display": "Medicaid PCCM (Primary Care Case Management)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "219",
				"display": "Medicaid Managed Care Other"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "22",
				"display": "Medicaid (Non-managed Care Plan)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "23",
				"display": "Medicaid/SCHIP"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "25",
				"display": "Medicaid - Out of State"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "26",
				"display": "Medicaid - Long Term Care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "29",
				"display": "Medicaid Other"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "291",
				"display": "Medicaid Pharmacy Benefit Manager"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "299",
				"display": "Medicaid - Dental"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3",
				"display": "OTHER GOVERNMENT (Federal/State/Local) (excluding Department of Corrections)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "31",
				"display": "Department of Defense"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "311",
				"display": "TRICARE (CHAMPUS)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3111",
				"display": "TRICARE Prime--HMO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3112",
				"display": "TRICARE Extra--PPO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3113",
				"display": "TRICARE Standard - Fee For Service"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3114",
				"display": "TRICARE For Life--Medicare Supplement"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3115",
				"display": "TRICARE Reserve Select"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3116",
				"display": "Uniformed Services Family Health Plan (USFHP) -- HMO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3119",
				"display": "Department of Defense - (other)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "312",
				"display": "Military Treatment Facility"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3121",
				"display": "Enrolled Prime--HMO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3122",
				"display": "Non-enrolled Space Available"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3123",
				"display": "TRICARE For Life (TFL)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "313",
				"display": "Dental --Stand Alone"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "32",
				"display": "Department of Veterans Affairs"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "321",
				"display": "Veteran care-Care provided to Veterans"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3211",
				"display": "Direct Care-Care provided in VA facilities"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3212",
				"display": "Indirect Care-Care provided outside VA facilities"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "32121",
				"display": "Fee Basis"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "32122",
				"display": "Foreign Fee/Foreign Medical Program (FMP)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "32123",
				"display": "Contract Nursing Home/Community Nursing Home"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "32124",
				"display": "State Veterans Home"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "32125",
				"display": "Sharing Agreements"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "32126",
				"display": "Other Federal Agency"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "32127",
				"display": "Dental Care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "32128",
				"display": "Vision Care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "322",
				"display": "Non-veteran care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3221",
				"display": "Civilian Health and Medical Program for the VA (CHAMPVA)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3222",
				"display": "Spina Bifida Health Care Program (SB)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3223",
				"display": "Children of Women Vietnam Veterans (CWVV)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3229",
				"display": "Other non-veteran care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "33",
				"display": "Indian Health Service or Tribe"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "331",
				"display": "Indian Health Service - Regular"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "332",
				"display": "Indian Health Service - Contract"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "333",
				"display": "Indian Health Service - Managed Care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "334",
				"display": "Indian Tribe - Sponsored Coverage"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "34",
				"display": "HRSA Program"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "341",
				"display": "Title V (MCH Block Grant)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "342",
				"display": "Migrant Health Program"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "343",
				"display": "Ryan White Act"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "349",
				"display": "Other"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "35",
				"display": "Black Lung"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "36",
				"display": "State Government"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "361",
				"display": "State SCHIP program (codes for individual states)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "362",
				"display": "Specific state programs (list/ local code)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "369",
				"display": "State, not otherwise specified (other state)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "37",
				"display": "Local Government"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "371",
				"display": "Local - Managed care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3711",
				"display": "HMO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3712",
				"display": "PPO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3713",
				"display": "POS"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "372",
				"display": "FFS/Indemnity"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "379",
				"display": "Local, not otherwise specified (other local, county)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "38",
				"display": "Other Government (Federal, State, Local not specified)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "381",
				"display": "Federal, State, Local not specified managed care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3811",
				"display": "Federal, State, Local not specified - HMO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3812",
				"display": "Federal, State, Local not specified - PPO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3813",
				"display": "Federal, State, Local not specified - POS"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "3819",
				"display": "Federal, State, Local not specified - not specified managed care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "382",
				"display": "Federal, State, Local not specified - FFS"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "389",
				"display": "Federal, State, Local not specified - Other"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "39",
				"display": "Other Federal"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "391",
				"display": "Federal Employee Health Plan - Use when known"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "4",
				"display": "DEPARTMENTS OF CORRECTIONS"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "41",
				"display": "Corrections Federal"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "42",
				"display": "Corrections State"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "43",
				"display": "Corrections Local"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "44",
				"display": "Corrections Unknown Level"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "5",
				"display": "PRIVATE HEALTH INSURANCE"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "51",
				"display": "Managed Care (Private)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "511",
				"display": "Commercial Managed Care - HMO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "512",
				"display": "Commercial Managed Care - PPO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "513",
				"display": "Commercial Managed Care - POS"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "514",
				"display": "Exclusive Provider Organization"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "515",
				"display": "Gatekeeper PPO (GPPO)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "516",
				"display": "Commercial Managed Care - Pharmacy Benefit Manager"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "517",
				"display": "Commercial Managed Care - Dental"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "519",
				"display": "Managed Care, Other (non HMO)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "52",
				"display": "Private Health Insurance - Indemnity"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "521",
				"display": "Commercial Indemnity"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "522",
				"display": "Self-insured (ERISA) Administrative Services Only (ASO) plan"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "523",
				"display": "Medicare supplemental policy (as second payer)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "524",
				"display": "Indemnity Insurance - Dental"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "529",
				"display": "Private health insurance--other commercial Indemnity"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "53",
				"display": "Managed Care (private) or private health insurance (indemnity), not otherwise specified"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "54",
				"display": "Organized Delivery System"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "55",
				"display": "Small Employer Purchasing Group"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "56",
				"display": "Specialized Stand-Alone Plan"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "561",
				"display": "Dental"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "562",
				"display": "Vision"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "59",
				"display": "Other Private Insurance"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "6",
				"display": "BLUE CROSS/BLUE SHIELD"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "61",
				"display": "BC Managed Care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "611",
				"display": "BC Managed Care - HMO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "612",
				"display": "BC Managed Care - PPO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "613",
				"display": "BC Managed Care - POS"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "614",
				"display": "BC Managed Care - Dental"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "619",
				"display": "BC Managed Care - Other"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "62",
				"display": "BC Insurance Indemnity"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "621",
				"display": "BC Indemnity"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "622",
				"display": "BC Self-insured (ERISA) Administrative Services Only (ASO)Plan"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "623",
				"display": "BC Medicare Supplemental Plan"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "629",
				"display": "BC Indemnity - Dental"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "7",
				"display": "MANAGED CARE, UNSPECIFIED (to be used only if one can't distinguish public from private)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "71",
				"display": "HMO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "72",
				"display": "PPO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "73",
				"display": "POS"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "79",
				"display": "Other Managed Care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "8",
				"display": "NO PAYMENT from an Organization/Agency/Program/Private Payer Listed"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "81",
				"display": "Self-pay (Includes applicants for insurance and Medicaid applicants)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "82",
				"display": "No Charge"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "821",
				"display": "Charity"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "822",
				"display": "Professional Courtesy"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "823",
				"display": "Research/Clinical Trial"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "83",
				"display": "Refusal to Pay/Bad Debt"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "84",
				"display": "Hill Burton Free Care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "85",
				"display": "Research/Donor"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "89",
				"display": "No Payment, Other"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "9",
				"display": "MISCELLANEOUS/OTHER"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "91",
				"display": "Foreign National"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "92",
				"display": "Other (Non-government)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "93",
				"display": "Disability Insurance"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "94",
				"display": "Long-term Care Insurance"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "95",
				"display": "Worker's Compensation"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "951",
				"display": "Worker's Comp HMO"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "953",
				"display": "Worker's Comp Fee-for-Service"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "954",
				"display": "Worker's Comp Other Managed Care"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "959",
				"display": "Worker's Comp, Other unspecified"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "96",
				"display": "Auto Insurance (includes no fault)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "97",
				"display": "Legal Liability / Liability Insurance"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "98",
				"display": "Other specified but not otherwise classifiable (includes Hospice - Unspecified plan)"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "99",
				"display": "No Typology Code available for payment source"
			  },
			  {
				"system": "https://nahdo.org/sopt",
				"version": "9.2",
				"code": "9999",
				"display": "Unavailable / No Payer Specified / Blank"
			  }
			]
		  }
		},
		"request": {
		  "method": "PUT",
		  "url": "ValueSet/2.16.840.1.114222.4.11.3591/_history/14"
		}
	  },
	  {
		"resource": {
		  "resourceType": "Measure",
		  "id": "NHSNdQMAcuteCareHospitalInitialPopulation",
		  "meta": {
			"profile": [
			  "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cohort-measure-cqfm",
			  "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/computable-measure-cqfm"
			]
		  },
		  "contained": [
			{
			  "resourceType": "Library",
			  "id": "effective-data-requirements",
			  "extension": [
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
					"code": "EMER",
					"display": "emergency"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
					"code": "ACUTE",
					"display": "inpatient acute"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
					"code": "IMP",
					"display": "inpatient encounter"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
					"code": "NONAC",
					"display": "inpatient non-acute"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
					"code": "SS",
					"display": "short stay"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/observation-category",
					"code": "laboratory",
					"display": "Laboratory"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/observation-category",
					"code": "vital-signs",
					"display": "Vital Signs"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://loinc.org",
					"code": "LP29684-5",
					"display": "Radiology"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://loinc.org",
					"code": "LP7839-6",
					"display": "Pathology"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://loinc.org",
					"code": "LP29708-2",
					"display": "Cardiology"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/v2-0074",
					"code": "LAB",
					"display": "Laboratory"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/observation-category",
					"code": "social-history",
					"display": "Social History"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/observation-category",
					"code": "survey",
					"display": "Survey"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/observation-category",
					"code": "imaging",
					"display": "Imaging"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-directReferenceCode",
				  "valueCoding": {
					"system": "http://terminology.hl7.org/CodeSystem/observation-category",
					"code": "procedure",
					"display": "Procedure"
				  }
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "Encounters"
					},
					{
					  "url": "statement",
					  "valueString": "define \"Encounters\":\n  [Encounter]"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 0
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "Qualifying Encounters During Measurement Period"
					},
					{
					  "url": "statement",
					  "valueString": "define \"Qualifying Encounters During Measurement Period\":\n ( [Encounter: \"Encounter Inpatient\"]\n  union [Encounter: \"Emergency Department Visit\"]\n  union [Encounter: \"Observation Services\"]\n  union [Encounter: class in {\"emergency\", \"inpatient acute\", \"inpatient encounter\", \"inpatient non-acute\", \"short stay\"}]) QualifyingEncounters\n  where QualifyingEncounters.status in {'in-progress', 'finished', 'triaged', 'onleave', 'entered-in-error'}\n    and QualifyingEncounters.period overlaps \"Measurement Period\""
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 1
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "Encounters with Patient Hospital Locations"
					},
					{
					  "url": "statement",
					  "valueString": "//potentially an issue as this may pull ALL EXISTING ENCOUNTERS (no period to look against)\ndefine \"Encounters with Patient Hospital Locations\":\n  \"Encounters\" Encounters\n  where exists(\n    Encounters.location EncounterLocation\n    where Global.GetLocation(EncounterLocation.location).type in \"Inpatient, Emergency, and Observation Locations\"\n      and EncounterLocation.period overlaps Encounters.period\n  )\n  and Encounters.status in {'in-progress', 'finished', 'triaged', 'onleave', 'entered-in-error'}"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 2
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "Initial Population"
					},
					{
					  "url": "statement",
					  "valueString": "// and Encounters.period overlaps \"Measurement Period\" (?)\n\ndefine \"Initial Population\":\n  \"Qualifying Encounters During Measurement Period\"\n  union \"Encounters with Patient Hospital Locations\""
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 3
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Encounter"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Encounter\": \n  \"Encounters\" Encounters\n  where exists(\n    \"Initial Population\" IP\n    where Encounters.period overlaps IP.period)\n  return SharedResource.EncounterResource(Encounters,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-encounter'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 4
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Medication Request"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Medication Request\":\n  [MedicationRequest] MedicationRequests \n  where exists(\n    \"Initial Population\" IP\n    where MedicationRequests.authoredOn during IP.period)\n  return SharedResource.MedicationRequestResource(MedicationRequests,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-medicationrequest'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 5
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Coverage"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Coverage\": \n\t[Coverage] Coverages\n  where exists(\n    \"Initial Population\" IP\n    where Coverages.period overlaps IP.period)\n  return SharedResource.CoverageResource(Coverages,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-coverage'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 6
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Procedure"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Procedure\":\n  [Procedure] Procedures \n  where exists(\n    \"Initial Population\" IP\n    where Global.\"Normalize Interval\"(Procedures.performed) overlaps IP.period)\n  return SharedResource.ProcedureResource(Procedures,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-procedure'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 7
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Device"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Device\":\n  [Device] Devices \n  where exists(\"Initial Population\")\n  return DeviceResource(Devices,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-device'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 8
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "Observations"
					},
					{
					  "url": "statement",
					  "valueString": "define \"Observations\":\n  [Observation]"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 9
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Observation Lab Category"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Observation Lab Category\":\n  \"Observations\" Observations \n  where (exists(Observations.category Category where Category ~ \"laboratory\"))\n    and exists(\n      \"Initial Population\" IP\n      where Global.\"Normalize Interval\"(Observations.effective) overlaps IP.period)\n  return SharedResource.ObservationLabResource(Observations,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-observation-lab'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 10
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Observation Vital Signs Category"
					},
					{
					  "url": "statement",
					  "valueString": "//Vital Signs Observation has its own profile in FHIR Base\ndefine \"SDE Observation Vital Signs Category\":\n  \"Observations\" Observations \n  where (exists(Observations.category Category where Category ~ \"vital-signs\"))\n    and exists(\n      \"Initial Population\" IP\n      where Global.\"Normalize Interval\"(Observations.effective) overlaps IP.period)\n  return ObservationVitalSignsResource(Observations,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-observation-vitals'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 11
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE DiagnosticReport Others"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE DiagnosticReport Others\":\n  [DiagnosticReport] DiagnosticReports\n  where not ((exists(DiagnosticReports.category Category where Category ~ \"Radiology\"))\n    or exists((DiagnosticReports.category Category where Category ~ \"Pathology\"))\n    or exists((DiagnosticReports.category Category where Category ~ \"Cardiology\"))\n    or exists(DiagnosticReports.category Category where Category ~ \"LAB\"))\n    and exists(\"Initial Population\" IP\n      where Global.\"Normalize Interval\"(DiagnosticReports.effective) overlaps IP.period)\n  return DiagnosticReportResource(DiagnosticReports,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-diagnosticreport'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 12
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Medication Administration"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Medication Administration\":\n  [MedicationAdministration] MedicationAdministrations \n  where exists(\n    \"Initial Population\" IP\n    where Global.\"Normalize Interval\"(MedicationAdministrations.effective) overlaps IP.period)\n  return SharedResource.MedicationAdministrationResource(MedicationAdministrations,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-medicationadministration'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 13
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Observation Category"
					},
					{
					  "url": "statement",
					  "valueString": "//Defaulting to base FHIR profile as there are no individual profiles in US Core 3.1.1 that cover these Observation categories\ndefine \"SDE Observation Category\":\n  \"Observations\" Observations \n  where ((exists(Observations.category Category where Category ~ \"social-history\"))\n    or (exists(Observations.category Category where Category ~ \"survey\"))\n    or (exists(Observations.category Category where Category ~ \"imaging\"))\n    or (exists(Observations.category Category where Category ~ \"procedure\")))\n    and exists(\n      \"Initial Population\" IP\n      where Global.\"Normalize Interval\"(Observations.effective) overlaps IP.period)\n  return ObservationResource(Observations,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-observation'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 14
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Condition"
					},
					{
					  "url": "statement",
					  "valueString": "//============================================================================\n//Supplemental Data Element\n//When FHIR.canonical value is present, US Core 3.1.1 profiles are used\n//When FHIR.canonical value is not present, FHIR Base profiles are used\n//============================================================================\ndefine \"SDE Condition\":\n  [Condition] Conditions \n  where exists(\"Initial Population\")\n  return SharedResource.ConditionResource(Conditions,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-condition'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 15
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "DiagnosticReports"
					},
					{
					  "url": "statement",
					  "valueString": "define \"DiagnosticReports\":\n  [DiagnosticReport]"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 16
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE DiagnosticReport Lab"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE DiagnosticReport Lab\":\n  \"DiagnosticReports\" DiagnosticReports\n  where (exists(DiagnosticReports.category Category where Category ~ \"LAB\")\n    and exists(\n      \"Initial Population\" IP\n      where Global.\"Normalize Interval\"(DiagnosticReports.effective) overlaps IP.period))\n  return SharedResource.DiagnosticReportLabResource(DiagnosticReports,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-diagnosticreport-lab'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 17
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "IP Encounters Overlap Measurement Period"
					},
					{
					  "url": "statement",
					  "valueString": "//Double checking for IP's period during MP as IP is created out of qualifying encounters, which checks for it, \n//and encounter's locations, which doesn't\ndefine \"IP Encounters Overlap Measurement Period\":\n  \"Initial Population\" IP\n  where IP.period overlaps \"Measurement Period\""
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 18
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "Get Locations from IP Encounters in Measurement Period"
					},
					{
					  "url": "statement",
					  "valueString": "define \"Get Locations from IP Encounters in Measurement Period\":\n  flatten(\"IP Encounters Overlap Measurement Period\" Encounters\n  let locationElements: Encounters.location\n  return\n    locationElements LE\n    let locationReference: LE.location\n    return Global.GetLocation(locationReference))"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 19
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Location"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Location\":\n  \"Get Locations from IP Encounters in Measurement Period\" Locations\n  where exists(\"Initial Population\")\n  and Locations is not null\n  return SharedResource.LocationResource(Locations,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-location'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 20
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Service Request"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Service Request\":\n  [ServiceRequest] ServiceRequests\n  where exists(\"Initial Population\" IP\n    where ServiceRequests.authoredOn during IP.period)\n  return SharedResource.ServiceRequestResource(ServiceRequests,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-servicerequest'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 21
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE DiagnosticReport Note"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE DiagnosticReport Note\":\n  \"DiagnosticReports\" DiagnosticReports\n  where ((exists(DiagnosticReports.category Category where Category ~ \"Radiology\"))\n    or exists((DiagnosticReports.category Category where Category ~ \"Pathology\"))\n    or exists((DiagnosticReports.category Category where Category ~ \"Cardiology\")))\n    and exists(\n      \"Initial Population\" IP\n      where Global.\"Normalize Interval\"(DiagnosticReports.effective) overlaps IP.period)\n  return DiagnosticReportResource(DiagnosticReports,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-diagnosticreport-note'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 22
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Minimal Patient"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Minimal Patient\":\n  Patient p\n  return SharedResource.PatientResource(p,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/cross-measure-patient'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 23
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Medication"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Medication\":\n  (\"SDE Medication Request\"\n  union \"SDE Medication Administration\") MedReqOrAdmin\n  where MedReqOrAdmin.medication is FHIR.Reference\n  and exists(\"Initial Population\") //No longer need to check for timing here because it's checked in SDE Medication Request/Administriation\n  return SharedResource.MedicationResource(GetMedicationFrom(MedReqOrAdmin.medication),\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-medication'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 24
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "SDE Specimen"
					},
					{
					  "url": "statement",
					  "valueString": "define \"SDE Specimen\":\n  [Specimen] Specimens\n  where exists(\n    \"Initial Population\" IP\n    where Global.\"Normalize Interval\"(Specimens.collection.collected) overlaps IP.period\n  )\n  return SharedResource.SpecimenResource(Specimens,\n  {FHIR.canonical{value: 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-specimen'}})"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 25
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "FHIRHelpers"
					},
					{
					  "url": "name",
					  "valueString": "ToString"
					},
					{
					  "url": "statement",
					  "valueString": "define function ToString(value EncounterStatus): value.value"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 26
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "FHIRHelpers"
					},
					{
					  "url": "name",
					  "valueString": "ToInterval"
					},
					{
					  "url": "statement",
					  "valueString": "define function ToInterval(period FHIR.Period):\n    if period is null then\n        null\n    else\n        if period.\"start\" is null then\n            Interval(period.\"start\".value, period.\"end\".value]\n        else\n            Interval[period.\"start\".value, period.\"end\".value]"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 27
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "MATGlobalCommonFunctionsFHIR4"
					},
					{
					  "url": "name",
					  "valueString": "GetLocation"
					},
					{
					  "url": "statement",
					  "valueString": "// Returns the location for the given location reference\n\n/*Returns the Location resource specified by the given reference*/\ndefine function \"GetLocation\"(reference Reference ):\n  singleton from (\n\t[Location] Locations\n\twhere Locations.id = GetId(reference.reference)\n  )"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 28
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "MATGlobalCommonFunctionsFHIR4"
					},
					{
					  "url": "name",
					  "valueString": "GetId"
					},
					{
					  "url": "statement",
					  "valueString": "/*Returns the tail of the given uri (i.e. everything after the last slash in the URI).*/\ndefine function \"GetId\"(uri String ):\n  Last(Split(uri, '/'))"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 29
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "FHIRHelpers"
					},
					{
					  "url": "name",
					  "valueString": "ToConcept"
					},
					{
					  "url": "statement",
					  "valueString": "define function ToConcept(concept FHIR.CodeableConcept):\n    if concept is null then\n        null\n    else\n        System.Concept {\n            codes: concept.coding C return ToCode(C),\n            display: concept.text.value\n        }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 30
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "EncounterResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function EncounterResource(encounter Encounter, profileURLs List<FHIR.canonical>):\n  encounter e\n  return Encounter{\n    id: FHIR.id{value: 'LCR-' + e.id},\n    meta: MetaElement(e, profileURLs),\n    extension: e.extension,\n    identifier: EncounterIdentifier(e.identifier),\n    status: e.status,\n    statusHistory: EncounterStatusHistory(e.statusHistory),\n    class: e.class,\n    classHistory: EncounterClassHistory(e.classHistory),\n    type: e.type,\n    serviceType: e.serviceType,\n    priority: e.priority,\n    subject: e.subject,\n    participant: EncounterParticipant(e.participant),\n    period: e.period,\n    length: e.length,\n    reasonCode: e.reasonCode,\n    reasonReference: e.reasonReference,\n    diagnosis: EncounterDiagnosis(e.diagnosis),\n    account: e.account,\n    hospitalization: EncounterHospitalization(e.hospitalization),\n    location: EncounterLocation(e.location),\n    partOf: e.partOf\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 31
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "MetaElement"
					},
					{
					  "url": "statement",
					  "valueString": "define function \"MetaElement\"(resource Resource, profileURLs List<FHIR.canonical>):\n  resource r\n  return FHIR.Meta{\n    extension: r.meta.extension,\n    versionId: r.meta.versionId,\n    lastUpdated: r.meta.lastUpdated,\n    profile: profileURLs,\n    security: r.meta.security,\n    tag: r.meta.tag\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 32
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "EncounterIdentifier"
					},
					{
					  "url": "statement",
					  "valueString": "define function EncounterIdentifier(identifier List<FHIR.Identifier>):\n  identifier i\n  return FHIR.Identifier{\n    use: i.use,\n    type: i.type,\n    system: i.system,\n    value: i.value,\n    period: i.period\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 33
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "EncounterStatusHistory"
					},
					{
					  "url": "statement",
					  "valueString": "define function EncounterStatusHistory(statusHistory List<FHIR.Encounter.StatusHistory>):\n  statusHistory sH\n  return FHIR.Encounter.StatusHistory{\n    status: sH.status,\n    period: sH.period\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 34
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "EncounterClassHistory"
					},
					{
					  "url": "statement",
					  "valueString": "define function EncounterClassHistory(classHistory List<FHIR.Encounter.ClassHistory>):\n  classHistory cH\n  return FHIR.Encounter.ClassHistory{\n    class: cH.class,\n    period: cH.period\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 35
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "EncounterParticipant"
					},
					{
					  "url": "statement",
					  "valueString": "define function EncounterParticipant(participant List<FHIR.Encounter.Participant>):\n  participant p\n  return FHIR.Encounter.Participant{\n    type: p.type,\n    period: p.period,\n    individual: p.individual\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 36
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "EncounterDiagnosis"
					},
					{
					  "url": "statement",
					  "valueString": "define function EncounterDiagnosis(diagnosis List<FHIR.Encounter.Diagnosis>):\n  diagnosis d\n  return FHIR.Encounter.Diagnosis{\n    condition: d.condition,\n    use: d.use,\n    rank: d.rank\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 37
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "EncounterHospitalization"
					},
					{
					  "url": "statement",
					  "valueString": "define function EncounterHospitalization(hospitalization FHIR.Encounter.Hospitalization):\n  hospitalization h\n  return FHIR.Encounter.Hospitalization{\n    preAdmissionIdentifier: h.preAdmissionIdentifier,\n    origin: h.origin,\n    admitSource: h.admitSource,\n    reAdmission: h.reAdmission,\n    dietPreference: h.dietPreference,\n    specialCourtesy: h.specialCourtesy,\n    specialArrangement: h.specialArrangement,\n    destination: h.destination,\n    dischargeDisposition: h.dischargeDisposition\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 38
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "EncounterLocation"
					},
					{
					  "url": "statement",
					  "valueString": "define function EncounterLocation(location List<FHIR.Encounter.Location>):\n  location l\n  return FHIR.Encounter.Location{\n    location: l.location,\n    status: l.status,\n    physicalType: l.physicalType,\n    period: l.period\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 39
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "FHIRHelpers"
					},
					{
					  "url": "name",
					  "valueString": "ToDateTime"
					},
					{
					  "url": "statement",
					  "valueString": "define function ToDateTime(value dateTime): value.value"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 40
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "MedicationRequestResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function MedicationRequestResource(medicationRequest MedicationRequest, profileURLs List<FHIR.canonical>):\n  medicationRequest m\n  return MedicationRequest{\n    id: FHIR.id {value: 'LCR-' + m.id},\n    meta: MetaElement(medicationRequest, profileURLs),\n    extension: m.extension,\n    status: m.status,\n    statusReason: m.statusReason,\n    intent: m.intent,\n    category: m.category,\n    priority: m.priority,\n    doNotPerform: m.doNotPerform,\n    reported: m.reported,\n    medication: m.medication,\n    subject: m.subject,\n    encounter: m.encounter,\n    authoredOn: m.authoredOn,\n    requester: m.requester,\n    recorder: m.recorder,\n    reasonCode: m.reasonCode,\n    reasonReference: m.reasonReference,\n    instantiatesCanonical: m.instantiatesCanonical,\n    instantiatesUri: m.instantiatesUri,\n    courseOfTherapyType: m.courseOfTherapyType,\n    dosageInstruction: MedicationRequestDosageInstruction(m.dosageInstruction)\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 41
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "MedicationRequestDosageInstruction"
					},
					{
					  "url": "statement",
					  "valueString": "define function MedicationRequestDosageInstruction(dosageInstruction List<FHIR.Dosage>):\n  dosageInstruction dI\n  return FHIR.Dosage{\n    text: dI.text,\n    patientInstruction: dI.patientInstruction,\n    timing: dI.timing,\n    asNeeded: dI.asNeeded,\n    site: dI.site,\n    route: dI.route,\n    method: dI.method,\n    doseAndRate: MedicationRequestDoseAndRate(dI.doseAndRate)\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 42
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "MedicationRequestDoseAndRate"
					},
					{
					  "url": "statement",
					  "valueString": "define function MedicationRequestDoseAndRate(doseAndRate List<FHIR.Dosage.DoseAndRate>):\n  doseAndRate dR\n  return FHIR.Dosage.DoseAndRate{\n    type: dR.type,\n    dose: dR.dose,\n    rate: dR.rate\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 43
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "CoverageResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function CoverageResource(coverage Coverage, profileURLs List<FHIR.canonical>):\n  coverage c\n  return Coverage{\n    id: FHIR.id{value: 'LCR-' + c.id},\n    meta: MetaElement(c, profileURLs),\n    extension: c.extension,\n    status: c.status,\n    type: c.type,\n    policyHolder: c.policyHolder,\n    subscriber: c.subscriber,\n    subscriberId: c.subscriberId,\n    beneficiary: c.beneficiary,\n    dependent: c.dependent,\n    relationship: c.relationship,\n    period: c.period,\n    payor: c.payor,\n    class: CoverageClass(c.class),\n    order: c.order,\n    network: c.network,\n    subrogation: c.subrogation,\n    contract: c.contract\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 44
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "CoverageClass"
					},
					{
					  "url": "statement",
					  "valueString": "define function CoverageClass(class List<FHIR.Coverage.Class>):\n  class c\n  return FHIR.Coverage.Class{\n    value: c.value,\n    name: c.name\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 45
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ProcedureResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function ProcedureResource(procedure Procedure, profileURLs List<FHIR.canonical>):\n  procedure p\n  return Procedure{\n    id: FHIR.id {value: 'LCR-' + p.id},\n    meta: MetaElement(p, profileURLs),\n    extension: p.extension,\n    instantiatesCanonical: p.instantiatesCanonical,\n    instantiatesUri: p.instantiatesUri,\n    basedOn: p.basedOn,\n    partOf: p.partOf,\n    status: p.status,\n    statusReason: p.statusReason,\n    category: p.category,\n    code: p.code,\n    subject: p.subject,\n    encounter: p.encounter,\n    performed: p.performed,\n    recorder: p.recorder,\n    asserter: p.asserter,\n    performer: ProcedurePerformer(p.performer),\n    location: p.location,\n    reasonCode: p.reasonCode,\n    reasonReference: p.reasonReference,\n    bodySite: p.bodySite,\n    outcome: p.outcome,\n    report: p.report,\n    complication: p.complication,\n    complicationDetail: p.complicationDetail,\n    followUp: p.followUp,\n    note: p.note,\n    focalDevice: ProcedureFocalDevice(p.focalDevice),\n    usedReference: p.usedReference,\n    usedCode: p.usedCode\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 46
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ProcedurePerformer"
					},
					{
					  "url": "statement",
					  "valueString": "define function ProcedurePerformer(performer List<FHIR.Procedure.Performer>):\n  performer p\n  return FHIR.Procedure.Performer{\n    function: p.function,\n    actor: p.actor,\n    onBehalfOf: p.onBehalfOf\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 47
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ProcedureFocalDevice"
					},
					{
					  "url": "statement",
					  "valueString": "define function ProcedureFocalDevice(device List<FHIR.Procedure.FocalDevice>):\n  device d\n  return FHIR.Procedure.FocalDevice{\n    action: d.action,\n    manipulated: d.manipulated\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 48
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "DeviceResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function DeviceResource(device Device, profileURLs List<FHIR.canonical>):\n  device d\n  return Device{\n    id: FHIR.id{value: 'LCR-' + d.id},\n    meta: SharedResource.MetaElement(d, profileURLs),\n    extension: d.extension,\n    definition: d.definition,\n    udiCarrier: DeviceUdiCarrier(d.udiCarrier),\n    status: d.status,\n    statusReason: d.statusReason,\n    distinctIdentifier: d.distinctIdentifier,\n    manufacturer: d.manufacturer,\n    manufactureDate: d.manufactureDate,\n    expirationDate: d.expirationDate,\n    lotNumber: d.lotNumber,\n    serialNumber: d.serialNumber,\n    deviceName: DeviceDeviceName(d.deviceName),\n    modelNumber: d.modelNumber,\n    partNumber: d.partNumber,\n    type: d.type,\n    specialization: DeviceSpecialization(d.specialization),\n    version: DeviceVersion(d.version),\n    property: DeviceProperty(d.property),\n    patient: d.patient,\n    owner: d.owner,\n    contact: d.contact,\n    location: d.location,\n    url: d.url,\n    note: d.note,\n    safety: d.safety,\n    parent: d.parent\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 49
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "DeviceUdiCarrier"
					},
					{
					  "url": "statement",
					  "valueString": "//\n//Measure Specific Resource Creation Functions\n//\ndefine function DeviceUdiCarrier(udiCarrier List<FHIR.Device.UdiCarrier>):\n  udiCarrier u\n  return FHIR.Device.UdiCarrier{\n    deviceIdentifier: u.deviceIdentifier,\n    issuer: u.issuer,\n    jurisdiction: u.jurisdiction,\n    carrierAIDC: u.carrierAIDC,\n    carrierHRF: u.carrierHRF,\n    entryType: u.entryType\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 50
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "DeviceDeviceName"
					},
					{
					  "url": "statement",
					  "valueString": "define function DeviceDeviceName(deviceName List<FHIR.Device.DeviceName>):\n  deviceName d\n  return FHIR.Device.DeviceName{\n    name: d.name,\n    type: d.type\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 51
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "DeviceSpecialization"
					},
					{
					  "url": "statement",
					  "valueString": "define function DeviceSpecialization(specialization List<FHIR.Device.Specialization>):\n  specialization s\n  return FHIR.Device.Specialization{\n    systemType: s.systemType,\n    version: s.version\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 52
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "DeviceVersion"
					},
					{
					  "url": "statement",
					  "valueString": "define function DeviceVersion(version List<FHIR.Device.Version>):\n  version v\n  return FHIR.Device.Version{\n    type: v.type,\n    component: v.component,\n    value: v.value\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 53
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "DeviceProperty"
					},
					{
					  "url": "statement",
					  "valueString": "define function DeviceProperty(deviceProperty List<FHIR.Device.Property>):\n  deviceProperty d\n  return FHIR.Device.Property{\n    id: d.id,\n    type: d.type,\n    valueQuantity: d.valueQuantity,\n    valueCode: d.valueCode\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 54
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ObservationLabResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function ObservationLabResource(observation Observation, profileURLs List<FHIR.canonical>):\n  observation o\n  return Observation{\n    id: FHIR.id {value: 'LCR-' + o.id},\n    meta: MetaElement(o, profileURLs),\n    extension: o.extension,\n    basedOn: o.basedOn,\n    partOf: o.partOf,\n    status: o.status,\n    category: ObservationLabCategory(o.category),\n    code: o.code,\n    subject: o.subject,\n    focus: o.focus,\n    encounter: o.encounter,\n    effective: o.effective,\n    issued: o.issued,\n    performer: o.performer,\n    value: o.value,\n    dataAbsentReason: o.dataAbsentReason,\n    interpretation: o.interpretation,\n    note: o.note,\n    bodySite: o.bodySite,\n    method: o.method,\n    specimen: o.specimen,\n    device: o.device,\n    referenceRange: ObservationReferenceRange(o.referenceRange),\n    hasMember: o.hasMember,\n    derivedFrom: o.derivedFrom,\n    component: ObservationComponent(o.component)\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 55
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ObservationLabCategory"
					},
					{
					  "url": "statement",
					  "valueString": "define function ObservationLabCategory(category List<CodeableConcept>):\n  category c\n  return CodeableConcept{\n    coding: ObservationLabCoding(c.coding),\n    text: c.text\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 56
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ObservationLabCoding"
					},
					{
					  "url": "statement",
					  "valueString": "define function ObservationLabCoding(coding List<Coding>):\n  coding c\n  return Coding{\n    id: c.id,\n    extension: c.extension,\n    system: c.system,\n    version: c.version,\n    code: c.code,\n    display: c.display,\n    userSelected: c.userSelected\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 57
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ObservationReferenceRange"
					},
					{
					  "url": "statement",
					  "valueString": "define function ObservationReferenceRange(referenceRange List<FHIR.Observation.ReferenceRange>):\n  referenceRange rR\n  return FHIR.Observation.ReferenceRange{\n    low: rR.low,\n    high: rR.high,\n    type: rR.type,\n    appliesTo: rR.appliesTo,\n    age: rR.age,\n    text: rR.text\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 58
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ObservationComponent"
					},
					{
					  "url": "statement",
					  "valueString": "define function ObservationComponent(component List<FHIR.Observation.Component>):\n  component c\n  return FHIR.Observation.Component{\n    code: c.code,\n    value: c.value,\n    dataAbsentReason: c.dataAbsentReason,\n    interpretation: c.interpretation,\n    referenceRange: c.referenceRange\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 59
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "ObservationVitalSignsResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function ObservationVitalSignsResource(observation Observation, profileURLs List<FHIR.canonical>):\n  observation o\n  return Observation{\n    id: FHIR.id {value: 'LCR-' + o.id},\n    meta: SharedResource.MetaElement(o, profileURLs),\n    extension: o.extension,\n    partOf: o.partOf,\n    status: o.status,\n    category: ObservationVitalSignsCategory(o.category),\n    code: o.code,\n    subject: o.subject,\n    focus: o.focus,\n    encounter: o.encounter,\n    effective: o.effective,\n    issued: o.issued,\n    performer: o.performer,\n    value: o.value,\n    dataAbsentReason: o.dataAbsentReason,\n    interpretation: o.interpretation,\n    note: o.note,\n    bodySite: o.bodySite,\n    method: o.method,\n    specimen: o.specimen,\n    device: o.device,\n    referenceRange: SharedResource.ObservationReferenceRange(o.referenceRange),\n    hasMember: o.hasMember,\n    derivedFrom: o.derivedFrom,\n    component: ObservationVitalSignsComponent(o.component)\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 60
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "ObservationVitalSignsCategory"
					},
					{
					  "url": "statement",
					  "valueString": "define function ObservationVitalSignsCategory(category List<CodeableConcept>):\n  category c\n  return CodeableConcept{\n    coding: ObservationVitalSignsCoding(c.coding),\n    text: c.text\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 61
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "ObservationVitalSignsCoding"
					},
					{
					  "url": "statement",
					  "valueString": "define function ObservationVitalSignsCoding(coding List<Coding>):\n  coding c\n  return Coding{\n    system: c.system,\n    version: c.version,\n    code: c.code,\n    display: c.display,\n    userSelected: c.userSelected\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 62
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "ObservationVitalSignsComponent"
					},
					{
					  "url": "statement",
					  "valueString": "define function ObservationVitalSignsComponent(component List<FHIR.Observation.Component>):\n  component c\n  return FHIR.Observation.Component{\n    code: c.code,\n    value: c.value,\n    dataAbsentReason: c.dataAbsentReason,\n    interpretation: c.interpretation,\n    referenceRange: SharedResource.ObservationReferenceRange(c.referenceRange)\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 63
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "DiagnosticReportResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function DiagnosticReportResource(diagnosticReport DiagnosticReport, profileURLs List<FHIR.canonical>):\n  diagnosticReport d\n  return DiagnosticReport{\n    id: FHIR.id{value: 'LCR-' + d.id},\n    meta: SharedResource.MetaElement(d, profileURLs),\n    extension: d.extension,\n    basedOn: d.basedOn,\n    status: d.status,\n    category: d.category,\n    code: d.code,\n    subject: d.subject,\n    encounter: d.encounter,\n    effective: d.effective,\n    issued: d.issued,\n    performer: d.performer,\n    resultsInterpreter: d.resultsInterpreter,\n    specimen: d.specimen,\n    result: d.result,\n    conclusion: d.conclusion,\n    conclusionCode: d.conclusionCode\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 64
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "MedicationAdministrationResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function MedicationAdministrationResource(medicationAdministration MedicationAdministration, profileURLs List<FHIR.canonical>):\n  medicationAdministration m\n  return MedicationAdministration{\n    id: FHIR.id {value: 'LCR-' + m.id},\n    meta: MetaElement(m, profileURLs),\n    extension: m.extension,\n    instantiates: m.instantiates,\n    partOf: m.partOf,\n    status: m.status,\n    statusReason: m.statusReason,\n    category: m.category,\n    medication: m.medication,\n    subject: m.subject,\n    context: m.context,\n    supportingInformation: m.supportingInformation,\n    effective: m.effective,\n    performer: MedicationAdministrationPerformer(m.performer),\n    reasonCode: m.reasonCode,\n    reasonReference: m.reasonReference,\n    request: m.request,\n    device: m.device,\n    note: m.note,\n    dosage: MedicationAdministrationDosage(m.dosage),\n    eventHistory: m.eventHistory\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 65
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "MedicationAdministrationPerformer"
					},
					{
					  "url": "statement",
					  "valueString": "define function MedicationAdministrationPerformer(performer List<FHIR.MedicationAdministration.Performer>):\n  performer p\n  return FHIR.MedicationAdministration.Performer{\n    function: p.function,\n    actor: p.actor\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 66
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "MedicationAdministrationDosage"
					},
					{
					  "url": "statement",
					  "valueString": "define function MedicationAdministrationDosage(dosage FHIR.MedicationAdministration.Dosage):\n  dosage d\n  return FHIR.MedicationAdministration.Dosage{\n    text: d.text,\n    site: d.site,\n    route: d.route,\n    method: d.method,\n    dose: d.dose,\n    rate: d.rate\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 67
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "ObservationResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function ObservationResource(observation Observation, profileURLs List<FHIR.canonical>):\n  observation o\n  return Observation{\n    id: FHIR.id {value: 'LCR-' + o.id},\n    meta: SharedResource.MetaElement(o, profileURLs),\n    extension: o.extension,\n    partOf: o.partOf,\n    status: o.status,\n    category: o.category,\n    code: o.code,\n    subject: o.subject,\n    focus: o.focus,\n    encounter: o.encounter,\n    effective: o.effective,\n    issued: o.issued,\n    performer: o.performer,\n    value: o.value,\n    dataAbsentReason: o.dataAbsentReason,\n    interpretation: o.interpretation,\n    note: o.note,\n    bodySite: o.bodySite,\n    method: o.method,\n    specimen: o.specimen,\n    device: o.device,\n    referenceRange: SharedResource.ObservationReferenceRange(o.referenceRange),\n    hasMember: o.hasMember,\n    derivedFrom: o.derivedFrom,\n    component: SharedResource.ObservationComponent(o.component)\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 68
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ConditionResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function ConditionResource(condition Condition, profileURLs List<FHIR.canonical>):\n  condition c\n  return Condition{\n    id: FHIR.id {value: 'LCR-' + c.id},\n    meta: MetaElement(c, profileURLs),\n    extension: c.extension,\n    clinicalStatus: c.clinicalStatus,\n    verificationStatus: c.verificationStatus,\n    category: c.category,\n    severity: c.severity,\n    code: c.code,\n    bodySite: c.bodySite,\n    subject: c.subject,\n    encounter: c.encounter,\n    onset: c.onset,\n    abatement: c.abatement,\n    recordedDate: c.recordedDate,\n    stage: ConditionStage(c.stage),\n    evidence: ConditionEvidence(c.evidence),\n    note: c.note\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 69
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ConditionStage"
					},
					{
					  "url": "statement",
					  "valueString": "define function ConditionStage(stage List<FHIR.Condition.Stage>):\n  stage s\n  return FHIR.Condition.Stage{\n    summary: s.summary,\n    assessment: s.assessment,\n    type: s.type\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 70
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ConditionEvidence"
					},
					{
					  "url": "statement",
					  "valueString": "define function ConditionEvidence(evidence List<FHIR.Condition.Evidence>):\n  evidence e\n  return FHIR.Condition.Evidence{\n    code: e.code,\n    detail: e.detail\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 71
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "DiagnosticReportLabResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function DiagnosticReportLabResource(diagnosticReport DiagnosticReport, profileURLs List<FHIR.canonical>):\n  diagnosticReport d\n  return DiagnosticReport{\n    id: FHIR.id{value: 'LCR-' + d.id},\n    meta: MetaElement(d, profileURLs),\n    extension: d.extension,\n    basedOn: d.basedOn,\n    status: d.status,\n    category: DiagnosticReportCategory(d.category),\n    code: d.code,\n    subject: d.subject,\n    encounter: d.encounter,\n    effective: d.effective,\n    issued: d.issued,\n    performer: d.performer,\n    resultsInterpreter: d.resultsInterpreter,\n    specimen: d.specimen,\n    result: d.result,\n    conclusion: d.conclusion,\n    conclusionCode: d.conclusionCode\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 72
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "DiagnosticReportCategory"
					},
					{
					  "url": "statement",
					  "valueString": "define function DiagnosticReportCategory(category List<CodeableConcept>):\n  category c\n  return CodeableConcept{\n    coding: DiagnosticReportCoding(c.coding)\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 73
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "DiagnosticReportCoding"
					},
					{
					  "url": "statement",
					  "valueString": "define function DiagnosticReportCoding(coding List<Coding>):\n  coding c\n  return Coding{\n    system: c.system,\n    version: c.version,\n    code: c.code,\n    display: c.display,\n    userSelected: c.userSelected\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 74
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "LocationResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function LocationResource(location Location, profileURLs List<FHIR.canonical>):\n  location l\n  return Location{\n    id: FHIR.id {value: 'LCR-' + l.id},\n    meta: MetaElement(l, profileURLs),\n    extension: l.extension,\n    status: l.status,\n    operationalStatus: l.operationalStatus,\n    name: l.name,\n    alias: l.alias,\n    description: l.description,\n    mode: l.mode,\n    type: l.type,\n    telecom: l.telecom,\n    address: LocationAddress(l.address),\n    physicalType: l.physicalType,\n    position: LocationPosition(l.position),\n    managingOrganization: l.managingOrganization,\n    partOf: l.partOf,\n    hoursOfOperation: LocationHoursOfOperation(l.hoursOfOperation),\n    availabilityExceptions: l.availabilityExceptions,\n    endpoint: l.endpoint\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 75
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "LocationAddress"
					},
					{
					  "url": "statement",
					  "valueString": "define function LocationAddress(address FHIR.Address):\n  address a\n  return FHIR.Address{\n    use: a.use,\n    type: a.type,\n    text: a.text,\n    line: a.line,\n    city: a.city,\n    district: a.district,\n    state: a.state,\n    postalCode: a.postalCode,\n    country: a.country,\n    period: a.period\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 76
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "LocationPosition"
					},
					{
					  "url": "statement",
					  "valueString": "define function LocationPosition(position FHIR.Location.Position):\n  position p\n  return FHIR.Location.Position{\n    longitude: p.longitude,\n    latitude: p.latitude,\n    altitude: p.altitude\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 77
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "LocationHoursOfOperation"
					},
					{
					  "url": "statement",
					  "valueString": "define function LocationHoursOfOperation(hoursOfOperation List<FHIR.Location.HoursOfOperation>):\n  hoursOfOperation hOO\n  return FHIR.Location.HoursOfOperation{\n    daysOfWeek: hOO.daysOfWeek,\n    allDay: hOO.allDay,\n    openingTime: hOO.openingTime,\n    closingTime: hOO.closingTime\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 78
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "ServiceRequestResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function ServiceRequestResource(serviceRequest ServiceRequest, profileURLs List<FHIR.canonical>):\n  serviceRequest sR\n  return ServiceRequest{\n    id: FHIR.id {value: 'LCR-' + sR.id},\n    meta: MetaElement(sR, profileURLs),\n    extension: sR.extension,\n    instantiatesCanonical: sR.instantiatesCanonical,\n    instantiatesUri: sR.instantiatesUri,\n    basedOn: sR.basedOn,\n    replaces: sR.replaces,\n    requisition: sR.requisition,\n    status: sR.status,\n    intent: sR.intent,\n    category: sR.category,\n    priority: sR.priority,\n    doNotPerform: sR.doNotPerform,\n    code: sR.code,\n    orderDetail: sR.orderDetail,\n    quantity: sR.quantity,\n    subject: sR.subject,\n    encounter: sR.encounter,\n    occurrence: sR.occurrence,\n    asNeeded: sR.asNeeded,\n    authoredOn: sR.authoredOn,\n    requester: sR.requester,\n    performerType: sR.performerType,\n    performer: sR.performer,\n    locationCode: sR.locationCode,\n    locationReference: sR.locationReference,\n    reasonCode: sR.reasonCode,\n    reasonReference: sR.reasonReference,\n    insurance: sR.insurance,\n    supportingInfo: sR.supportingInfo,\n    specimen: sR.specimen,\n    bodySite: sR.bodySite,\n    note: sR.note,\n    patientInstruction: sR.patientInstruction,\n    relevantHistory: sR.relevantHistory\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 79
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "PatientResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function PatientResource(patient Patient, profileURLs List<FHIR.canonical>):\n  patient p\n  return Patient{\n    id: FHIR.id{value: 'LCR-' + p.id},\n    meta: MetaElement(p, profileURLs),\n    extension: GetPatientExtensions(p) union GetIdExtensions(p),\n    identifier: PatientIdentifier(p.identifier),\n    active: p.active,\n    name: PatientName(p.name),\n    telecom: PatientTelecom(p.telecom),\n    gender: p.gender,\n    birthDate: p.birthDate,\n    deceased: p.deceased,\n    address: PatientAddress(p.address),\n    maritalStatus: p.maritalStatus,\n    multipleBirth: p.multipleBirth,\n    photo: p.photo,\n    contact: PatientContact(p.contact),\n    communication: PatientCommunication(p.communication),\n    generalPractitioner: p.generalPractitioner,\n    managingOrganization: p.managingOrganization,\n    link: PatientLink(p.link)\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 80
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "GetPatientExtensions"
					},
					{
					  "url": "statement",
					  "valueString": "define function \"GetPatientExtensions\"(domainResource DomainResource):\n  domainResource.extension E\n  where E.url = 'http://hl7.org/fhir/us/core/StructureDefinition/us-core-race'\n    or E.url = 'http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity'\n    or E.url = 'http://hl7.org/fhir/us/core/StructureDefinition/us-core-birthsex'\n    or E.url = 'http://hl7.org/fhir/us/core/StructureDefinition/us-core-genderIdentity'\n    or E.url = 'http://hl7.org/fhir/StructureDefinition/patient-genderIdentity'\n    or E.url = 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-original-resource-id-extension'\n  return E"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 81
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "GetIdExtensions"
					},
					{
					  "url": "statement",
					  "valueString": "define function \"GetIdExtensions\"(domainResource DomainResource):\n  domainResource.extension E\n  where E.url = 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-original-resource-id-extension'\n  return E"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 82
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "PatientIdentifier"
					},
					{
					  "url": "statement",
					  "valueString": "define function PatientIdentifier(identifier List<FHIR.Identifier>):\n  identifier i\n  return FHIR.Identifier{\n    id: i.id,\n    extension: i.extension,\n    use: i.use,\n    type: i.type,\n    system: i.system,\n    value: i.value,\n    period: i.period,\n    assigner: i.assigner\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 83
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "PatientName"
					},
					{
					  "url": "statement",
					  "valueString": "define function PatientName(name List<FHIR.HumanName>):\n  name n\n  return FHIR.HumanName{\n    id: n.id,\n    extension: n.extension,\n    use: n.use,\n    text: n.text,\n    family: n.family,\n    given: n.given,\n    prefix: n.prefix,\n    suffix: n.suffix,\n    period: n.period\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 84
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "PatientTelecom"
					},
					{
					  "url": "statement",
					  "valueString": "define function PatientTelecom(telecom List<FHIR.ContactPoint>):\n  telecom t\n  return FHIR.ContactPoint{\n    system: t.system,\n    value: t.value,\n    use: t.use,\n    rank: t.rank,\n    period: t.period\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 85
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "PatientAddress"
					},
					{
					  "url": "statement",
					  "valueString": "define function PatientAddress(address List<FHIR.Address>):\n  address a\n  return FHIR.Address{\n    id: a.id,\n    extension: a.extension,\n    use: a.use,\n    type: a.type,\n    text: a.text,\n    line: a.line,\n    city: a.city,\n    district: a.district,\n    state: a.state,\n    postalCode: a.postalCode,\n    country: a.country,\n    period: a.period\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 86
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "PatientContact"
					},
					{
					  "url": "statement",
					  "valueString": "define function PatientContact(contact List<FHIR.Patient.Contact>):\n  contact c\n  return FHIR.Patient.Contact{\n    id: c.id,\n    extension: c.extension,\n    relationship: c.relationship,\n    name: c.name,\n    telecom: c.telecom,\n    address: c.address,\n    gender: c.gender,\n    organization: c.organization,\n    period: c.period\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 87
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "PatientCommunication"
					},
					{
					  "url": "statement",
					  "valueString": "define function PatientCommunication(communication List<FHIR.Patient.Communication>):\n  communication c\n  return FHIR.Patient.Communication{\n    id: c.id,\n    extension: c.extension,\n    language: c.language,\n    preferred: c.preferred\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 88
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "PatientLink"
					},
					{
					  "url": "statement",
					  "valueString": "define function PatientLink(link List<FHIR.Patient.Link>):\n  link l\n  return FHIR.Patient.Link{\n    id: l.id,\n    extension: l.extension,\n    modifierExtension: l.modifierExtension,\n    other: l.other,\n    type: l.type\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 89
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "MedicationResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function MedicationResource(medication Medication, profileURLs List<FHIR.canonical>):\n  medication m\n  return Medication{\n    id: FHIR.id {value: 'LCR-' + m.id},\n    meta: MetaElement(m, profileURLs),\n    extension: m.extension,\n    code: m.code,\n    status: m.status,\n    manufacturer: m.manufacturer,\n    form: m.form,\n    amount: m.amount,\n    ingredient: MedicationIngredient(m.ingredient),\n    batch: MedicationBatch(m.batch)\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 90
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "MedicationIngredient"
					},
					{
					  "url": "statement",
					  "valueString": "define function MedicationIngredient(ingredient List<FHIR.Medication.Ingredient>):\n  ingredient i\n  return FHIR.Medication.Ingredient{\n    item: i.item,\n    strength: i.strength\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 91
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "MedicationBatch"
					},
					{
					  "url": "statement",
					  "valueString": "define function MedicationBatch(batch FHIR.Medication.Batch):\n  batch b\n  return FHIR.Medication.Batch{\n    lotNumber: b.lotNumber,\n    expirationDate: b.expirationDate\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 92
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "GetMedicationFrom"
					},
					{
					  "url": "statement",
					  "valueString": "//\n//Functions\n//\ndefine function \"GetMedicationFrom\"(choice Choice<FHIR.CodeableConcept, FHIR.Reference>):\n  case\n    when choice is FHIR.Reference then\n      GetMedication(choice as FHIR.Reference)\n    else\n      null\n  end"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 93
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "NHSNdQMAcuteCareHospitalInitialPopulation"
					},
					{
					  "url": "name",
					  "valueString": "GetMedication"
					},
					{
					  "url": "statement",
					  "valueString": "define function \"GetMedication\"(reference Reference ):\n  singleton from (\n    [Medication] Medications\n    where Medications.id = Global.GetId(reference.reference)\n  )"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 94
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "SpecimenResource"
					},
					{
					  "url": "statement",
					  "valueString": "define function SpecimenResource(specimen Specimen, profileURLs List<FHIR.canonical>):\n  specimen s\n  return Specimen{\n    id: FHIR.id {value: 'LCR-' + s.id},\n    meta: MetaElement(s, profileURLs),\n    extension: s.extension,\n    identifier: s.identifier,\n    accessionIdentifier: s.accessionIdentifier,\n    status: s.status,\n    type: s.type,\n    subject: s.subject,\n    receivedTime: s.receivedTime,\n    parent: s.parent,\n    request: s.request,\n    collection: SpecimenCollection(s.collection),\n    processing: SpecimenProcessing(s.processing),\n    container: SpecimenContainer(s.container),\n    condition: s.condition,\n    note: s.note\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 95
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "SpecimenCollection"
					},
					{
					  "url": "statement",
					  "valueString": "define function SpecimenCollection(collection FHIR.Specimen.Collection):\n  collection c\n  return FHIR.Specimen.Collection{\n    collector: c.collector,\n    collected: c.collected,\n    //duration: c.duration, Does not parse for some reason? Need to bring up with SmileCDR\n    quantity: c.quantity,\n    method: c.method,\n    bodySite: c.bodySite,\n    fastingStatus: c.fastingStatus\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 96
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "SpecimenProcessing"
					},
					{
					  "url": "statement",
					  "valueString": "define function SpecimenProcessing(processing List<FHIR.Specimen.Processing>):\n  processing p\n  return FHIR.Specimen.Processing{\n    description: p.description,\n    procedure: p.procedure,\n    additive: p.additive,\n    time: p.time\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 97
					}
				  ]
				},
				{
				  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-logicDefinition",
				  "extension": [
					{
					  "url": "libraryName",
					  "valueString": "SharedResourceCreation"
					},
					{
					  "url": "name",
					  "valueString": "SpecimenContainer"
					},
					{
					  "url": "statement",
					  "valueString": "define function SpecimenContainer(container List<FHIR.Specimen.Container>):\n  container c\n  return FHIR.Specimen.Container{\n    description: c.description,\n    type: c.type,\n    capacity: c.capacity,\n    specimenQuantity: c.specimenQuantity,\n    additive: c.additive\n  }"
					},
					{
					  "url": "displaySequence",
					  "valueInteger": 98
					}
				  ]
				}
			  ],
			  "name": "EffectiveDataRequirements",
			  "status": "active",
			  "type": {
				"coding": [
				  {
					"system": "http://terminology.hl7.org/CodeSystem/library-type",
					"code": "module-definition"
				  }
				]
			  },
			  "relatedArtifact": [
				{
				  "type": "depends-on",
				  "display": "Library FHIRHelpers",
				  "resource": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/FHIRHelpers|4.0.1"
				},
				{
				  "type": "depends-on",
				  "display": "Library Global",
				  "resource": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/MATGlobalCommonFunctionsFHIR4|6.1.000"
				},
				{
				  "type": "depends-on",
				  "display": "Library SharedResource",
				  "resource": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/SharedResourceCreation|0.1.005"
				},
				{
				  "type": "depends-on",
				  "display": "Code system ActCode",
				  "resource": "http://terminology.hl7.org/CodeSystem/v3-ActCode"
				},
				{
				  "type": "depends-on",
				  "display": "Code system Observation Category",
				  "resource": "http://terminology.hl7.org/CodeSystem/observation-category"
				},
				{
				  "type": "depends-on",
				  "display": "Code system LOINC",
				  "resource": "http://loinc.org"
				},
				{
				  "type": "depends-on",
				  "display": "Code system V2-0074",
				  "resource": "http://terminology.hl7.org/CodeSystem/v2-0074"
				},
				{
				  "type": "depends-on",
				  "display": "Value set Encounter Inpatient",
				  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307"
				},
				{
				  "type": "depends-on",
				  "display": "Value set Emergency Department Visit",
				  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.117.1.7.1.292"
				},
				{
				  "type": "depends-on",
				  "display": "Value set Observation Services",
				  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1111.143"
				},
				{
				  "type": "depends-on",
				  "display": "Value set Inpatient, Emergency, and Observation Locations",
				  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1046.265"
				}
			  ],
			  "parameter": [
				{
				  "name": "Measurement Period",
				  "use": "in",
				  "min": 0,
				  "max": "1",
				  "type": "Period"
				},
				{
				  "name": "SDE Encounter",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Encounter"
				},
				{
				  "name": "SDE Medication Request",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "MedicationRequest"
				},
				{
				  "name": "SDE Coverage",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Coverage"
				},
				{
				  "name": "SDE Procedure",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Procedure"
				},
				{
				  "name": "SDE Device",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Device"
				},
				{
				  "name": "SDE Observation Lab Category",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Observation"
				},
				{
				  "name": "SDE Observation Vital Signs Category",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Observation"
				},
				{
				  "name": "SDE DiagnosticReport Others",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "DiagnosticReport"
				},
				{
				  "name": "SDE Medication Administration",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "MedicationAdministration"
				},
				{
				  "name": "SDE Observation Category",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Observation"
				},
				{
				  "name": "SDE Condition",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Condition"
				},
				{
				  "name": "Initial Population",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Encounter"
				},
				{
				  "name": "SDE DiagnosticReport Lab",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "DiagnosticReport"
				},
				{
				  "name": "SDE Location",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Location"
				},
				{
				  "name": "SDE Service Request",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "ServiceRequest"
				},
				{
				  "name": "SDE DiagnosticReport Note",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "DiagnosticReport"
				},
				{
				  "name": "SDE Minimal Patient",
				  "use": "out",
				  "min": 0,
				  "max": "1",
				  "type": "Patient"
				},
				{
				  "name": "SDE Medication",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Medication"
				},
				{
				  "name": "SDE Specimen",
				  "use": "out",
				  "min": 0,
				  "max": "*",
				  "type": "Specimen"
				}
			  ],
			  "dataRequirement": [
				{
				  "type": "Encounter",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Encounter"
				  ],
				  "mustSupport": [
					"type",
					"status",
					"period",
					"id",
					"extension",
					"identifier",
					"statusHistory",
					"class",
					"classHistory",
					"serviceType",
					"priority",
					"subject",
					"participant",
					"length",
					"reasonCode",
					"reasonReference",
					"diagnosis",
					"account",
					"hospitalization",
					"location",
					"partOf"
				  ],
				  "codeFilter": [
					{
					  "path": "type",
					  "valueSet": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307"
					}
				  ]
				},
				{
				  "type": "Encounter",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Encounter"
				  ],
				  "mustSupport": [
					"type",
					"status",
					"period",
					"id",
					"extension",
					"identifier",
					"statusHistory",
					"class",
					"classHistory",
					"serviceType",
					"priority",
					"subject",
					"participant",
					"length",
					"reasonCode",
					"reasonReference",
					"diagnosis",
					"account",
					"hospitalization",
					"location",
					"partOf"
				  ],
				  "codeFilter": [
					{
					  "path": "type",
					  "valueSet": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.117.1.7.1.292"
					}
				  ]
				},
				{
				  "type": "Encounter",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Encounter"
				  ],
				  "mustSupport": [
					"type",
					"status",
					"period",
					"id",
					"extension",
					"identifier",
					"statusHistory",
					"class",
					"classHistory",
					"serviceType",
					"priority",
					"subject",
					"participant",
					"length",
					"reasonCode",
					"reasonReference",
					"diagnosis",
					"account",
					"hospitalization",
					"location",
					"partOf"
				  ],
				  "codeFilter": [
					{
					  "path": "type",
					  "valueSet": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1111.143"
					}
				  ]
				},
				{
				  "type": "Encounter",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Encounter"
				  ],
				  "mustSupport": [
					"class",
					"status",
					"period",
					"id",
					"extension",
					"identifier",
					"statusHistory",
					"classHistory",
					"type",
					"serviceType",
					"priority",
					"subject",
					"participant",
					"length",
					"reasonCode",
					"reasonReference",
					"diagnosis",
					"account",
					"hospitalization",
					"location",
					"partOf"
				  ],
				  "codeFilter": [
					{
					  "path": "class",
					  "code": [
						{
						  "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
						  "code": "EMER",
						  "display": "emergency"
						},
						{
						  "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
						  "code": "ACUTE",
						  "display": "inpatient acute"
						},
						{
						  "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
						  "code": "IMP",
						  "display": "inpatient encounter"
						},
						{
						  "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
						  "code": "NONAC",
						  "display": "inpatient non-acute"
						},
						{
						  "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
						  "code": "SS",
						  "display": "short stay"
						}
					  ]
					}
				  ]
				},
				{
				  "type": "Encounter",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Encounter"
				  ],
				  "mustSupport": [
					"status",
					"period",
					"id",
					"extension",
					"identifier",
					"statusHistory",
					"class",
					"classHistory",
					"type",
					"serviceType",
					"priority",
					"subject",
					"participant",
					"length",
					"reasonCode",
					"reasonReference",
					"diagnosis",
					"account",
					"hospitalization",
					"location",
					"partOf"
				  ]
				},
				{
				  "type": "Location",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Location"
				  ],
				  "mustSupport": [
					"id",
					"extension",
					"status",
					"operationalStatus",
					"name",
					"alias",
					"description",
					"mode",
					"type",
					"telecom",
					"address",
					"physicalType",
					"position",
					"managingOrganization",
					"partOf",
					"hoursOfOperation",
					"availabilityExceptions",
					"endpoint"
				  ]
				},
				{
				  "type": "MedicationRequest",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/MedicationRequest"
				  ],
				  "mustSupport": [
					"authoredOn",
					"id",
					"extension",
					"status",
					"statusReason",
					"intent",
					"category",
					"priority",
					"doNotPerform",
					"reported",
					"medication",
					"subject",
					"encounter",
					"requester",
					"recorder",
					"reasonCode",
					"reasonReference",
					"instantiatesCanonical",
					"instantiatesUri",
					"courseOfTherapyType",
					"dosageInstruction"
				  ]
				},
				{
				  "type": "Coverage",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Coverage"
				  ],
				  "mustSupport": [
					"period",
					"id",
					"extension",
					"status",
					"type",
					"policyHolder",
					"subscriber",
					"subscriberId",
					"beneficiary",
					"dependent",
					"relationship",
					"payor",
					"class",
					"order",
					"network",
					"subrogation",
					"contract"
				  ]
				},
				{
				  "type": "Procedure",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Procedure"
				  ],
				  "mustSupport": [
					"performed",
					"id",
					"extension",
					"instantiatesCanonical",
					"instantiatesUri",
					"basedOn",
					"partOf",
					"status",
					"statusReason",
					"category",
					"code",
					"subject",
					"encounter",
					"recorder",
					"asserter",
					"performer",
					"location",
					"reasonCode",
					"reasonReference",
					"bodySite",
					"outcome",
					"report",
					"complication",
					"complicationDetail",
					"followUp",
					"note",
					"focalDevice",
					"usedReference",
					"usedCode"
				  ]
				},
				{
				  "type": "Device",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Device"
				  ],
				  "mustSupport": [
					"id",
					"extension",
					"definition",
					"udiCarrier",
					"status",
					"statusReason",
					"distinctIdentifier",
					"manufacturer",
					"manufactureDate",
					"expirationDate",
					"lotNumber",
					"serialNumber",
					"deviceName",
					"modelNumber",
					"partNumber",
					"type",
					"specialization",
					"version",
					"property",
					"patient",
					"owner",
					"contact",
					"location",
					"url",
					"note",
					"safety",
					"parent"
				  ]
				},
				{
				  "type": "Observation",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Observation"
				  ],
				  "mustSupport": [
					"id",
					"extension",
					"basedOn",
					"partOf",
					"status",
					"category",
					"code",
					"subject",
					"focus",
					"encounter",
					"effective",
					"issued",
					"performer",
					"value",
					"dataAbsentReason",
					"interpretation",
					"note",
					"bodySite",
					"method",
					"specimen",
					"device",
					"referenceRange",
					"hasMember",
					"derivedFrom",
					"component"
				  ]
				},
				{
				  "type": "DiagnosticReport",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/DiagnosticReport"
				  ],
				  "mustSupport": [
					"category",
					"effective",
					"id",
					"extension",
					"basedOn",
					"status",
					"code",
					"subject",
					"encounter",
					"issued",
					"performer",
					"resultsInterpreter",
					"specimen",
					"result",
					"conclusion",
					"conclusionCode"
				  ]
				},
				{
				  "type": "MedicationAdministration",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/MedicationAdministration"
				  ],
				  "mustSupport": [
					"effective",
					"id",
					"extension",
					"instantiates",
					"partOf",
					"status",
					"statusReason",
					"category",
					"medication",
					"subject",
					"context",
					"supportingInformation",
					"performer",
					"reasonCode",
					"reasonReference",
					"request",
					"device",
					"note",
					"dosage",
					"eventHistory"
				  ]
				},
				{
				  "type": "Condition",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Condition"
				  ],
				  "mustSupport": [
					"id",
					"extension",
					"clinicalStatus",
					"verificationStatus",
					"category",
					"severity",
					"code",
					"bodySite",
					"subject",
					"encounter",
					"onset",
					"abatement",
					"recordedDate",
					"stage",
					"evidence",
					"note"
				  ]
				},
				{
				  "type": "ServiceRequest",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/ServiceRequest"
				  ],
				  "mustSupport": [
					"authoredOn",
					"id",
					"extension",
					"instantiatesCanonical",
					"instantiatesUri",
					"basedOn",
					"replaces",
					"requisition",
					"status",
					"intent",
					"category",
					"priority",
					"doNotPerform",
					"code",
					"orderDetail",
					"quantity",
					"subject",
					"encounter",
					"occurrence",
					"asNeeded",
					"requester",
					"performerType",
					"performer",
					"locationCode",
					"locationReference",
					"reasonCode",
					"reasonReference",
					"insurance",
					"supportingInfo",
					"specimen",
					"bodySite",
					"note",
					"patientInstruction",
					"relevantHistory"
				  ]
				},
				{
				  "type": "Patient",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Patient"
				  ],
				  "mustSupport": [
					"id",
					"identifier",
					"active",
					"name",
					"telecom",
					"gender",
					"birthDate",
					"deceased",
					"address",
					"maritalStatus",
					"multipleBirth",
					"photo",
					"contact",
					"communication",
					"generalPractitioner",
					"managingOrganization",
					"link"
				  ]
				},
				{
				  "type": "Medication",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Medication"
				  ],
				  "mustSupport": [
					"id",
					"extension",
					"code",
					"status",
					"manufacturer",
					"form",
					"amount",
					"ingredient",
					"batch"
				  ]
				},
				{
				  "type": "Specimen",
				  "profile": [
					"http://hl7.org/fhir/StructureDefinition/Specimen"
				  ],
				  "mustSupport": [
					"collection",
					"collection.collected",
					"id",
					"extension",
					"identifier",
					"accessionIdentifier",
					"status",
					"type",
					"subject",
					"receivedTime",
					"parent",
					"request",
					"processing",
					"container",
					"condition",
					"note"
				  ]
				}
			  ]
			}
		  ],
		  "extension": [
			{
			  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-populationBasis",
			  "valueCode": "Encounter"
			},
			{
			  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-softwaresystem",
			  "valueReference": {
				"reference": "Device/cqf-tooling"
			  }
			},
			{
			  "id": "effective-data-requirements",
			  "url": "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-effectiveDataRequirements",
			  "valueReference": {
				"reference": "#effective-data-requirements"
			  }
			}
		  ],
		  "url": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Measure/NHSNdQMAcuteCareHospitalInitialPopulation",
		  "identifier": [
			{
			  "system": "https://nhsnlink.org",
			  "value": "NHSNdQMAcuteCareHospitalInitialPopulation"
			}
		  ],
		  "version": "0.0.014",
		  "name": "NHSNdQMAcuteCareHospitalInitialPopulation",
		  "title": "NHSN dQM Acute Care Hospital Initial Population",
		  "status": "draft",
		  "experimental": false,
		  "date": "2024-02-29T17:44:38-05:00",
		  "publisher": "Lantana Consulting Group",
		  "description": "The Acute Care Hospital Initial Population includes all encounters for patients of any age in an ED, observation, or inpatient location or all encounters for patients of any age with an ED, observation, inpatient, or short stay status during the measurement period.",
		  "copyright": "Limited proprietary coding is contained in the Measure specifications for user convenience. Users of proprietary code sets should obtain all necessary licenses from the owners of the code sets.",
		  "relatedArtifact": [
			{
			  "type": "documentation",
			  "display": "https://www.cdc.gov/nhsn/index.html [placeholder for link to protocol on CDC website] ",
			  "url": "https://www.cdc.gov/nhsn/index.html",
			  "document": {
				"url": "https://www.cdc.gov/nhsn/index.html"
			  }
			}
		  ],
		  "library": [
			"http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/NHSNdQMAcuteCareHospitalInitialPopulation"
		  ],
		  "disclaimer": "This performance measure is not a clinical guideline, does not establish a standard of medical care and has not been tested for all potential applications.        THE MEASURES AND SPECIFICATIONS ARE PROVIDED “AS IS” WITHOUT WARRANTY OF ANY KIND.        This measure and specifications are subject to further revisions.",
		  "scoring": {
			"coding": [
			  {
				"system": "http://terminology.hl7.org/CodeSystem/measure-scoring",
				"code": "cohort",
				"display": "Cohort"
			  }
			]
		  },
		  "type": [
			{
			  "coding": [
				{
				  "system": "http://terminology.hl7.org/CodeSystem/measure-type",
				  "code": "outcome",
				  "display": "Outcome"
				}
			  ]
			}
		  ],
		  "rationale": "The NHSN Acute Care Hospital dQM allows for facilities to report line level patient data electronically to NHSN for the following modules that are reported monthly: Glycemic Control, Hypoglycemia; Healthcare facility-onset, antibiotic-Treated Clostridioides difficile (C. difficile) Infection (HT-CDI); Hospital-Onset Bacteremia & Fungemia (HOB); Venous Thromboembolism (VTE); Late Onset Sepsis / Meningitis. *Please see [Acute Care / Critical Access Hospitals (ACH) | NHSN | CDC](https://www.cdc.gov/nhsn/acute-care-hospital/index.html) for the individual measure protocols.",
		  "group": [
			{
			  "population": [
				{
				  "id": "initial-population",
				  "code": {
					"coding": [
					  {
						"system": "http://terminology.hl7.org/CodeSystem/measure-population",
						"code": "initial-population",
						"display": "Initial Population"
					  }
					]
				  },
				  "description": "All encounters for patients of any age in an ED, observation, or inpatient location or all encounters for patients of any age with an ED, observation, inpatient, or short stay status during the measurement period.",
				  "criteria": {
					"language": "text/cql-identifier",
					"expression": "Initial Population"
				  }
				}
			  ]
			}
		  ],
		  "supplementalData": [
			{
			  "id": "sde-condition",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Condition",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Condition"
			  }
			},
			{
			  "id": "sde-device",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Device",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Device"
			  }
			},
			{
			  "id": "sde-diagnosticreport-lab",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE DiagnosticReport Lab",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE DiagnosticReport Lab"
			  }
			},
			{
			  "id": "sde-diagnosticreport-note",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE DiagnosticReport Note",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE DiagnosticReport Note"
			  }
			},
			{
			  "id": "sde-diagnosticreport-others",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE DiagnosticReport Others",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE DiagnosticReport Others"
			  }
			},
			{
			  "id": "sde-encounter",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Encounter",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Encounter"
			  }
			},
			{
			  "id": "sde-location",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Location",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Location"
			  }
			},
			{
			  "id": "sde-medication-administration",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Medication Administration",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Medication Administration"
			  }
			},
			{
			  "id": "sde-medication-request",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Medication Request",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Medication Request"
			  }
			},
			{
			  "id": "sde-medication",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Medication",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Medication"
			  }
			},
			{
			  "id": "sde-observation-lab-category",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Observation Lab Category",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Observation Lab Category"
			  }
			},
			{
			  "id": "sde-observation-vital-signs-category",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Observation Vital Signs Category",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Observation Vital Signs Category"
			  }
			},
			{
			  "id": "sde-observation-category",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Observation Category",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Observation Category"
			  }
			},
			{
			  "id": "sde-coverage",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Coverage",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Coverage"
			  }
			},
			{
			  "id": "sde-procedure",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Procedure",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Procedure"
			  }
			},
			{
			  "id": "sde-specimen",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Specimen",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Specimen"
			  }
			},
			{
			  "id": "sde-service-request",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Service Request",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Service Request"
			  }
			},
			{
			  "id": "sde-minimal-patient",
			  "usage": [
				{
				  "coding": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/measure-data-usage",
					  "code": "supplemental-data"
					}
				  ]
				}
			  ],
			  "description": "SDE Minimal Patient",
			  "criteria": {
				"language": "text/cql-identifier",
				"expression": "SDE Minimal Patient"
			  }
			}
		  ]
		},
		"request": {
		  "method": "PUT",
		  "url": "Measure/NHSNdQMAcuteCareHospitalInitialPopulation"
		}
	  },
	  {
		"resource": {
		  "resourceType": "ValueSet",
		  "id": "2.16.840.1.113883.3.117.1.7.1.292",
		  "meta": {
			"versionId": "34",
			"lastUpdated": "2021-06-11T01:02:23.000-04:00"
		  },
		  "text": {
			"status": "generated",
			"div": "<div xmlns=\"http://www.w3.org/1999/xhtml\">\n         <h3>Value Set Contents</h3>\n         <p>This value set contains 1 concepts</p>\n         <p>All codes from system \n            <code>http://snomed.info/sct</code>\n         </p>\n         <table class=\"codes\">\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <b>Code</b>\n               </td>\n               <td>\n                  <b>Display</b>\n               </td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"http---snomed.info-sct-4525004\"> </a>\n                  <a href=\"http://browser.ihtsdotools.org/?perspective=full&amp;conceptId1=4525004\">4525004</a>\n               </td>\n               <td>Emergency department patient visit (procedure)</td>\n            </tr>\n         </table>\n      </div>"
		  },
		  "url": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.117.1.7.1.292",
		  "version": "20210611",
		  "name": "Emergency Department Visit",
		  "title": "Emergency Department Visit",
		  "status": "active",
		  "date": "2021-06-11T01:02:23-04:00",
		  "publisher": "TJC EH Steward",
		  "description": "The purpose of this value set is to represent concepts for encounters in the emergency department (ED).",
		  "expansion": {
			"identifier": "urn:uuid:8a4ca8c5-3324-49c6-b9f3-61b45192e630",
			"timestamp": "2022-03-09T10:10:51-05:00",
			"total": 1,
			"offset": 0,
			"parameter": [
			  {
				"name": "count",
				"valueInteger": 1000
			  },
			  {
				"name": "offset",
				"valueInteger": 0
			  }
			],
			"contains": [
			  {
				"system": "http://snomed.info/sct",
				"version": "http://snomed.info/sct/731000124108/version/20210901",
				"code": "4525004",
				"display": "Emergency department patient visit (procedure)"
			  }
			]
		  }
		},
		"request": {
		  "method": "PUT",
		  "url": "ValueSet/2.16.840.1.113883.3.117.1.7.1.292/_history/34"
		}
	  },
	  {
		"resource": {
		  "resourceType": "Library",
		  "id": "MATGlobalCommonFunctionsFHIR4",
		  "contained": [
			{
			  "resourceType": "Parameters",
			  "id": "options",
			  "parameter": [
				{
				  "name": "translatorVersion",
				  "valueString": "3.5.1"
				},
				{
				  "name": "option",
				  "valueString": "EnableDateRangeOptimization"
				},
				{
				  "name": "option",
				  "valueString": "EnableAnnotations"
				},
				{
				  "name": "option",
				  "valueString": "EnableLocators"
				},
				{
				  "name": "option",
				  "valueString": "DisableListDemotion"
				},
				{
				  "name": "option",
				  "valueString": "DisableListPromotion"
				},
				{
				  "name": "analyzeDataRequirements",
				  "valueBoolean": false
				},
				{
				  "name": "collapseDataRequirements",
				  "valueBoolean": true
				},
				{
				  "name": "compatibilityLevel",
				  "valueString": "1.5"
				},
				{
				  "name": "enableCqlOnly",
				  "valueBoolean": false
				},
				{
				  "name": "errorLevel",
				  "valueString": "Info"
				},
				{
				  "name": "signatureLevel",
				  "valueString": "None"
				},
				{
				  "name": "validateUnits",
				  "valueBoolean": true
				},
				{
				  "name": "verifyOnly",
				  "valueBoolean": false
				}
			  ]
			}
		  ],
		  "extension": [
			{
			  "url": "http://hl7.org/fhir/StructureDefinition/cqf-cqlOptions",
			  "valueReference": {
				"reference": "#options"
			  }
			}
		  ],
		  "url": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/MATGlobalCommonFunctionsFHIR4",
		  "version": "6.1.000",
		  "name": "MATGlobalCommonFunctionsFHIR4",
		  "status": "draft",
		  "type": {
			"coding": [
			  {
				"system": "http://terminology.hl7.org/CodeSystem/library-type",
				"code": "logic-library"
			  }
			]
		  },
		  "relatedArtifact": [
			{
			  "type": "depends-on",
			  "display": "FHIR model information",
			  "resource": "http://fhir.org/guides/cqf/common/Library/FHIR-ModelInfo|4.0.1"
			},
			{
			  "type": "depends-on",
			  "display": "Library FHIRHelpers",
			  "resource": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/FHIRHelpers|4.0.1"
			},
			{
			  "type": "depends-on",
			  "display": "Code system ConditionClinicalStatusCodes",
			  "resource": "http://terminology.hl7.org/CodeSystem/condition-clinical"
			},
			{
			  "type": "depends-on",
			  "display": "Code system AllergyIntoleranceClinicalStatusCodes",
			  "resource": "http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical"
			},
			{
			  "type": "depends-on",
			  "display": "Code system AllergyIntoleranceVerificationStatusCodes",
			  "resource": "http://terminology.hl7.org/CodeSystem/allergyintolerance-verification"
			},
			{
			  "type": "depends-on",
			  "display": "Code system Diagnosis Role",
			  "resource": "http://terminology.hl7.org/CodeSystem/diagnosis-role"
			},
			{
			  "type": "depends-on",
			  "display": "Code system LOINC",
			  "resource": "http://loinc.org"
			},
			{
			  "type": "depends-on",
			  "display": "Code system MedicationRequestCategory",
			  "resource": "http://terminology.hl7.org/CodeSystem/medicationrequest-category"
			},
			{
			  "type": "depends-on",
			  "display": "Code system ConditionVerificationStatusCodes",
			  "resource": "http://terminology.hl7.org/CodeSystem/condition-ver-status"
			},
			{
			  "type": "depends-on",
			  "display": "Code system SNOMEDCT",
			  "resource": "http://snomed.info/sct"
			},
			{
			  "type": "depends-on",
			  "display": "Code system RoleCode",
			  "resource": "http://terminology.hl7.org/CodeSystem/v3-RoleCode"
			},
			{
			  "type": "depends-on",
			  "display": "Value set Emergency Department Visit",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.117.1.7.1.292"
			},
			{
			  "type": "depends-on",
			  "display": "Value set Encounter Inpatient",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307"
			},
			{
			  "type": "depends-on",
			  "display": "Value set Observation Services",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1111.143"
			}
		  ],
		  "parameter": [
			{
			  "name": "Measurement Period",
			  "use": "in",
			  "min": 0,
			  "max": "1",
			  "type": "Period"
			},
			{
			  "name": "Patient",
			  "use": "out",
			  "min": 0,
			  "max": "1",
			  "type": "Patient"
			},
			{
			  "name": "Inpatient Encounter",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Encounter"
			}
		  ],
		  "dataRequirement": [
			{
			  "type": "Patient",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Patient"
			  ]
			},
			{
			  "type": "Encounter",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Encounter"
			  ],
			  "mustSupport": [
				"type",
				"status",
				"period",
				"condition",
				"condition.reference",
				"rank"
			  ],
			  "codeFilter": [
				{
				  "path": "type",
				  "valueSet": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307"
				}
			  ]
			},
			{
			  "type": "Encounter",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Encounter"
			  ],
			  "mustSupport": [
				"type",
				"period",
				"condition",
				"condition.reference",
				"rank"
			  ],
			  "codeFilter": [
				{
				  "path": "type",
				  "valueSet": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1111.143"
				}
			  ]
			},
			{
			  "type": "Encounter",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Encounter"
			  ],
			  "mustSupport": [
				"type",
				"status",
				"period",
				"condition",
				"condition.reference",
				"rank"
			  ],
			  "codeFilter": [
				{
				  "path": "type",
				  "valueSet": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.117.1.7.1.292"
				}
			  ]
			},
			{
			  "type": "Condition",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Condition"
			  ],
			  "mustSupport": [
				"id"
			  ]
			},
			{
			  "type": "Location",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Location"
			  ],
			  "mustSupport": [
				"id"
			  ]
			},
			{
			  "type": "Provenance",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Provenance"
			  ],
			  "mustSupport": [
				"target"
			  ],
			  "codeFilter": [
				{
				  "path": "target"
				}
			  ]
			}
		  ],
		  "content": [
			{
			  "contentType": "text/cql",
			  "data": "bGlicmFyeSBNQVRHbG9iYWxDb21tb25GdW5jdGlvbnNGSElSNCB2ZXJzaW9uICc2LjEuMDAwJw0KDQovKkB1cGRhdGU6IEJUUiAyMDIwLTAzLTMxIC0+DQpJbmNyZW1lbnRlZCB2ZXJzaW9uIHRvIDUuMC4wMDANClVwZGF0ZWQgRkhJUiB2ZXJzaW9uIHRvIDQuMC4xDQpDaGFuZ2VkIHRpbWV6b25lIGtleXdvcmQgdG8gdGltZXpvbmVvZmZzZXQgZm9yIHVzZSB3aXRoIENRTCAxLjQNClJlbW92ZWQgTm9ybWFsaXplIE9uc2V0IGluIGZhdm9yIG9mIG1vcmUgZ2VuZXJhbCBOb3JtYWxpemUgSW50ZXJ2YWwNClVwZGF0ZWQgQ29kZVN5c3RlbXMgZm9yIENvbmRpdGlvblZlcmlmaWNhdGlvblN0YXR1c0NvZGVzIGFuZCBSb2xlQ29kZXMNCg0KQHVwZGF0ZTogQlRSIDIwMjEtMDUtMTMgLT4NCkFkZGVkIEFjdGl2ZUNvbmRpdGlvbiBDb2RlcyBhbmQgSW5hY3RpdmUgQ29uZGl0aW9uIENvZGVzIHZhbHVlIHNldHMNCkFkZGVkIGZ1bmN0aW9uIGRvY3VtZW50YXRpb24gdGhyb3VnaG91dA0KRml4ZWQgRURWaXNpdCBub3QgdXNpbmcgTGFzdA0KVXBkYXRlZCBwcmV2YWxlbmNlIHBlcmlvZCB0byB1c2UgYW4gaW5jbHVzaXZlIGJvdW5kYXJ5IGlmIHRoZSBjb25kaXRpb24gaXMgYWN0aXZlDQpBZGRlZCBIYXNTdGFydCwgSGFzRW5kLCBFYXJsaWVzdCwgYW5kIExhdGVzdCBmdW5jdGlvbnMNClJlbW92ZWQgVG9EYXRlIGFuZCBBZ2UgY2FsY3VsYXRpb24gZnVuY3Rpb25zDQoNCkB1cGRhdGU6IEJUUiAyMDIxLTA2LTI1IC0+DQpBZGRlZCBHZXRCYXNlRXh0ZW5zaW9uIG92ZXJsb2FkcyBmb3IgRWxlbWVudCovDQoNCnVzaW5nIEZISVIgdmVyc2lvbiAnNC4wLjEnDQoNCmluY2x1ZGUgRkhJUkhlbHBlcnMgdmVyc2lvbiAnNC4wLjEnIGNhbGxlZCBGSElSSGVscGVycw0KDQpjb2Rlc3lzdGVtICJDb25kaXRpb25DbGluaWNhbFN0YXR1c0NvZGVzIjogJ2h0dHA6Ly90ZXJtaW5vbG9neS5obDcub3JnL0NvZGVTeXN0ZW0vY29uZGl0aW9uLWNsaW5pY2FsJyANCmNvZGVzeXN0ZW0gIkFsbGVyZ3lJbnRvbGVyYW5jZUNsaW5pY2FsU3RhdHVzQ29kZXMiOiAnaHR0cDovL3Rlcm1pbm9sb2d5LmhsNy5vcmcvQ29kZVN5c3RlbS9hbGxlcmd5aW50b2xlcmFuY2UtY2xpbmljYWwnIA0KY29kZXN5c3RlbSAiQWxsZXJneUludG9sZXJhbmNlVmVyaWZpY2F0aW9uU3RhdHVzQ29kZXMiOiAnaHR0cDovL3Rlcm1pbm9sb2d5LmhsNy5vcmcvQ29kZVN5c3RlbS9hbGxlcmd5aW50b2xlcmFuY2UtdmVyaWZpY2F0aW9uJyANCmNvZGVzeXN0ZW0gIkRpYWdub3NpcyBSb2xlIjogJ2h0dHA6Ly90ZXJtaW5vbG9neS5obDcub3JnL0NvZGVTeXN0ZW0vZGlhZ25vc2lzLXJvbGUnIA0KY29kZXN5c3RlbSAiTE9JTkMiOiAnaHR0cDovL2xvaW5jLm9yZycgDQpjb2Rlc3lzdGVtICJNZWRpY2F0aW9uUmVxdWVzdENhdGVnb3J5IjogJ2h0dHA6Ly90ZXJtaW5vbG9neS5obDcub3JnL0NvZGVTeXN0ZW0vbWVkaWNhdGlvbnJlcXVlc3QtY2F0ZWdvcnknIA0KY29kZXN5c3RlbSAiQ29uZGl0aW9uVmVyaWZpY2F0aW9uU3RhdHVzQ29kZXMiOiAnaHR0cDovL3Rlcm1pbm9sb2d5LmhsNy5vcmcvQ29kZVN5c3RlbS9jb25kaXRpb24tdmVyLXN0YXR1cycgDQpjb2Rlc3lzdGVtICJTTk9NRURDVCI6ICdodHRwOi8vc25vbWVkLmluZm8vc2N0JyANCmNvZGVzeXN0ZW0gIlJvbGVDb2RlIjogJ2h0dHA6Ly90ZXJtaW5vbG9neS5obDcub3JnL0NvZGVTeXN0ZW0vdjMtUm9sZUNvZGUnIA0KDQp2YWx1ZXNldCAiRW1lcmdlbmN5IERlcGFydG1lbnQgVmlzaXQiOiAnaHR0cDovL2N0cy5ubG0ubmloLmdvdi9maGlyL1ZhbHVlU2V0LzIuMTYuODQwLjEuMTEzODgzLjMuMTE3LjEuNy4xLjI5MicgDQp2YWx1ZXNldCAiRW5jb3VudGVyIElucGF0aWVudCI6ICdodHRwOi8vY3RzLm5sbS5uaWguZ292L2ZoaXIvVmFsdWVTZXQvMi4xNi44NDAuMS4xMTM4ODMuMy42NjYuNS4zMDcnIA0KdmFsdWVzZXQgIk9ic2VydmF0aW9uIFNlcnZpY2VzIjogJ2h0dHA6Ly9jdHMubmxtLm5paC5nb3YvZmhpci9WYWx1ZVNldC8yLjE2Ljg0MC4xLjExMzc2Mi4xLjQuMTExMS4xNDMnIA0KDQpjb2RlICJhY3RpdmUiOiAnYWN0aXZlJyBmcm9tICJDb25kaXRpb25DbGluaWNhbFN0YXR1c0NvZGVzIiBkaXNwbGF5ICdhY3RpdmUnDQpjb2RlICJhbGxlcmd5LWFjdGl2ZSI6ICdhY3RpdmUnIGZyb20gIkFsbGVyZ3lJbnRvbGVyYW5jZUNsaW5pY2FsU3RhdHVzQ29kZXMiIGRpc3BsYXkgJ2FsbGVyZ3ktYWN0aXZlJw0KY29kZSAiYWxsZXJneS1jb25maXJtZWQiOiAnY29uZmlybWVkJyBmcm9tICJBbGxlcmd5SW50b2xlcmFuY2VWZXJpZmljYXRpb25TdGF0dXNDb2RlcyIgZGlzcGxheSAnYWxsZXJneS1jb25maXJtZWQnDQpjb2RlICJhbGxlcmd5LWluYWN0aXZlIjogJ2luYWN0aXZlJyBmcm9tICJBbGxlcmd5SW50b2xlcmFuY2VDbGluaWNhbFN0YXR1c0NvZGVzIiBkaXNwbGF5ICdhbGxlcmd5LWluYWN0aXZlJw0KY29kZSAiYWxsZXJneS1yZWZ1dGVkIjogJ3JlZnV0ZWQnIGZyb20gIkFsbGVyZ3lJbnRvbGVyYW5jZVZlcmlmaWNhdGlvblN0YXR1c0NvZGVzIiBkaXNwbGF5ICdhbGxlcmd5LXJlZnV0ZWQnDQpjb2RlICJhbGxlcmd5LXJlc29sdmVkIjogJ3Jlc29sdmVkJyBmcm9tICJBbGxlcmd5SW50b2xlcmFuY2VDbGluaWNhbFN0YXR1c0NvZGVzIiBkaXNwbGF5ICdhbGxlcmd5LXJlc29sdmVkJw0KY29kZSAiYWxsZXJneS11bmNvbmZpcm1lZCI6ICd1bmNvbmZpcm1lZCcgZnJvbSAiQWxsZXJneUludG9sZXJhbmNlVmVyaWZpY2F0aW9uU3RhdHVzQ29kZXMiIGRpc3BsYXkgJ2FsbGVyZ3ktdW5jb25maXJtZWQnDQpjb2RlICJCaWxsaW5nIjogJ2JpbGxpbmcnIGZyb20gIkRpYWdub3NpcyBSb2xlIiBkaXNwbGF5ICdCaWxsaW5nJw0KY29kZSAiQmlydGhkYXRlIjogJzIxMTEyLTgnIGZyb20gIkxPSU5DIiBkaXNwbGF5ICdCaXJ0aCBkYXRlJw0KY29kZSAiQ29tbXVuaXR5IjogJ2NvbW11bml0eScgZnJvbSAiTWVkaWNhdGlvblJlcXVlc3RDYXRlZ29yeSIgZGlzcGxheSAnQ29tbXVuaXR5Jw0KY29kZSAiY29uZmlybWVkIjogJ2NvbmZpcm1lZCcgZnJvbSAiQ29uZGl0aW9uVmVyaWZpY2F0aW9uU3RhdHVzQ29kZXMiIGRpc3BsYXkgJ2NvbmZpcm1lZCcNCmNvZGUgIkRlYWQiOiAnNDE5MDk5MDA5JyBmcm9tICJTTk9NRURDVCIgZGlzcGxheSAnRGVhZCcNCmNvZGUgImRpZmZlcmVudGlhbCI6ICdkaWZmZXJlbnRpYWwnIGZyb20gIkNvbmRpdGlvblZlcmlmaWNhdGlvblN0YXR1c0NvZGVzIiBkaXNwbGF5ICdkaWZmZXJlbnRpYWwnDQpjb2RlICJEaXNjaGFyZ2UiOiAnZGlzY2hhcmdlJyBmcm9tICJNZWRpY2F0aW9uUmVxdWVzdENhdGVnb3J5IiBkaXNwbGF5ICdEaXNjaGFyZ2UnDQpjb2RlICJlbnRlcmVkLWluLWVycm9yIjogJ2VudGVyZWQtaW4tZXJyb3InIGZyb20gIkNvbmRpdGlvblZlcmlmaWNhdGlvblN0YXR1c0NvZGVzIiBkaXNwbGF5ICdlbnRlcmVkLWluLWVycm9yJw0KY29kZSAiRVIiOiAnRVInIGZyb20gIlJvbGVDb2RlIiBkaXNwbGF5ICdFbWVyZ2VuY3kgcm9vbScNCmNvZGUgIklDVSI6ICdJQ1UnIGZyb20gIlJvbGVDb2RlIiBkaXNwbGF5ICdJbnRlbnNpdmUgY2FyZSB1bml0Jw0KY29kZSAiaW5hY3RpdmUiOiAnaW5hY3RpdmUnIGZyb20gIkNvbmRpdGlvbkNsaW5pY2FsU3RhdHVzQ29kZXMiIGRpc3BsYXkgJ2luYWN0aXZlJw0KY29kZSAicHJvdmlzaW9uYWwiOiAncHJvdmlzaW9uYWwnIGZyb20gIkNvbmRpdGlvblZlcmlmaWNhdGlvblN0YXR1c0NvZGVzIiBkaXNwbGF5ICdwcm92aXNpb25hbCcNCmNvZGUgInJlY3VycmVuY2UiOiAncmVjdXJyZW5jZScgZnJvbSAiQ29uZGl0aW9uQ2xpbmljYWxTdGF0dXNDb2RlcyIgZGlzcGxheSAncmVjdXJyZW5jZScNCmNvZGUgInJlZnV0ZWQiOiAncmVmdXRlZCcgZnJvbSAiQ29uZGl0aW9uVmVyaWZpY2F0aW9uU3RhdHVzQ29kZXMiIGRpc3BsYXkgJ3JlZnV0ZWQnDQpjb2RlICJyZWxhcHNlIjogJ3JlbGFwc2UnIGZyb20gIkNvbmRpdGlvbkNsaW5pY2FsU3RhdHVzQ29kZXMiIGRpc3BsYXkgJ3JlbGFwc2UnDQpjb2RlICJyZW1pc3Npb24iOiAncmVtaXNzaW9uJyBmcm9tICJDb25kaXRpb25DbGluaWNhbFN0YXR1c0NvZGVzIiBkaXNwbGF5ICdyZW1pc3Npb24nDQpjb2RlICJyZXNvbHZlZCI6ICdyZXNvbHZlZCcgZnJvbSAiQ29uZGl0aW9uQ2xpbmljYWxTdGF0dXNDb2RlcyIgZGlzcGxheSAncmVzb2x2ZWQnDQpjb2RlICJ1bmNvbmZpcm1lZCI6ICd1bmNvbmZpcm1lZCcgZnJvbSAiQ29uZGl0aW9uVmVyaWZpY2F0aW9uU3RhdHVzQ29kZXMiIGRpc3BsYXkgJ3VuY29uZmlybWVkJw0KDQpwYXJhbWV0ZXIgIk1lYXN1cmVtZW50IFBlcmlvZCIgSW50ZXJ2YWw8RGF0ZVRpbWU+DQogIGRlZmF1bHQgSW50ZXJ2YWxbQDIwMTktMDEtMDFUMDA6MDA6MDAuMCwgQDIwMjAtMDEtMDFUMDA6MDA6MDAuMCkNCg0KY29udGV4dCBQYXRpZW50DQoNCmRlZmluZSAiSW5wYXRpZW50IEVuY291bnRlciI6DQogIFtFbmNvdW50ZXI6ICJFbmNvdW50ZXIgSW5wYXRpZW50Il0gRW5jb3VudGVySW5wYXRpZW50DQogICAgICAJCXdoZXJlIEVuY291bnRlcklucGF0aWVudC5zdGF0dXMgPSAnZmluaXNoZWQnDQogICAgICAJCSAgICBhbmQgIkxlbmd0aEluRGF5cyIoRW5jb3VudGVySW5wYXRpZW50LnBlcmlvZCkgPD0gMTIwDQogICAgICAJCQlhbmQgRW5jb3VudGVySW5wYXRpZW50LnBlcmlvZCBlbmRzIGR1cmluZyAiTWVhc3VyZW1lbnQgUGVyaW9kIg0KDQovKkNhbGN1bGF0ZXMgdGhlIGRpZmZlcmVuY2UgaW4gY2FsZW5kYXIgZGF5cyBiZXR3ZWVuIHRoZSBzdGFydCBhbmQgZW5kIG9mIHRoZSBnaXZlbiBpbnRlcnZhbC4qLw0KZGVmaW5lIGZ1bmN0aW9uICJMZW5ndGhJbkRheXMiKFZhbHVlIEludGVydmFsPERhdGVUaW1lPiApOg0KICBkaWZmZXJlbmNlIGluIGRheXMgYmV0d2VlbiBzdGFydCBvZiBWYWx1ZSBhbmQgZW5kIG9mIFZhbHVlDQoNCi8qUmV0dXJucyB0aGUgbW9zdCByZWNlbnQgZW1lcmdlbmN5IGRlcGFydG1lbnQgdmlzaXQsIGlmIGFueSwgdGhhdCBvY2N1cnMgMSBob3VyIG9yIGxlc3MgcHJpb3IgdG8gdGhlIGdpdmVuIGVuY291bnRlci4qLw0KZGVmaW5lIGZ1bmN0aW9uICJFRCBWaXNpdCIoVGhlRW5jb3VudGVyIEZISVIuRW5jb3VudGVyICk6DQogIExhc3QoDQogICAgW0VuY291bnRlcjogIkVtZXJnZW5jeSBEZXBhcnRtZW50IFZpc2l0Il0gRURWaXNpdA0KICAgICAgd2hlcmUgRURWaXNpdC5zdGF0dXMgPSAnZmluaXNoZWQnDQogICAgICAgIGFuZCBFRFZpc2l0LnBlcmlvZCBlbmRzIDEgaG91ciBvciBsZXNzIG9uIG9yIGJlZm9yZSBzdGFydCBvZiBGSElSSGVscGVycy5Ub0ludGVydmFsKFRoZUVuY291bnRlci5wZXJpb2QpDQogICAgICBzb3J0IGJ5IGVuZCBvZiBwZXJpb2QNCiAgICApDQoNCi8qSG9zcGl0YWxpemF0aW9uIHJldHVybnMgdGhlIHRvdGFsIGludGVydmFsIGZvciBhZG1pc3Npb24gdG8gZGlzY2hhcmdlIGZvciB0aGUgZ2l2ZW4gZW5jb3VudGVyLCBvciBmb3IgdGhlIGFkbWlzc2lvbiBvZiBhbnkgaW1tZWRpYXRlbHkgcHJpb3IgZW1lcmdlbmN5IGRlcGFydG1lbnQgdmlzaXQgdG8gdGhlIGRpc2NoYXJnZSBvZiB0aGUgZ2l2ZW4gZW5jb3VudGVyLiovDQpkZWZpbmUgZnVuY3Rpb24gIkhvc3BpdGFsaXphdGlvbiIoVGhlRW5jb3VudGVyIEZISVIuRW5jb3VudGVyICk6DQogICggIkVEIFZpc2l0IihUaGVFbmNvdW50ZXIpICkgWA0KICAgIHJldHVybg0KICAgICAgICBpZiBYIGlzIG51bGwgdGhlbiBUaGVFbmNvdW50ZXIucGVyaW9kDQogICAgICAgIGVsc2UgSW50ZXJ2YWxbc3RhcnQgb2YgRkhJUkhlbHBlcnMuVG9JbnRlcnZhbChYLnBlcmlvZCksIGVuZCBvZiBGSElSSGVscGVycy5Ub0ludGVydmFsKFRoZUVuY291bnRlci5wZXJpb2QpXQ0KDQovKlJldHVybnMgbGlzdCBvZiBhbGwgbG9jYXRpb25zIHdpdGhpbiBhbiBlbmNvdW50ZXIsIGluY2x1ZGluZyBsb2NhdGlvbnMgZm9yIGltbWVkaWF0ZWx5IHByaW9yIEVEIHZpc2l0LiovDQpkZWZpbmUgZnVuY3Rpb24gIkhvc3BpdGFsaXphdGlvbiBMb2NhdGlvbnMiKFRoZUVuY291bnRlciBGSElSLkVuY291bnRlciApOg0KICAoICJFRCBWaXNpdCIoVGhlRW5jb3VudGVyKSApIEVERW5jb3VudGVyDQogICAgcmV0dXJuDQogICAgICAgIGlmIEVERW5jb3VudGVyIGlzIG51bGwgdGhlbiBUaGVFbmNvdW50ZXIubG9jYXRpb24NCiAgICAgICAgZWxzZSBmbGF0dGVuIHsgRURFbmNvdW50ZXIubG9jYXRpb24sIFRoZUVuY291bnRlci5sb2NhdGlvbiB9DQoNCi8qUmV0dXJucyB0aGUgbGVuZ3RoIG9mIHN0YXkgaW4gZGF5cyAoaS5lLiB0aGUgbnVtYmVyIG9mIGRheXMgYmV0d2VlbiBhZG1pc3Npb24gYW5kIGRpc2NoYXJnZSkgZm9yIHRoZSBnaXZlbiBlbmNvdW50ZXIsIG9yIGZyb20gdGhlIGFkbWlzc2lvbiBvZiBhbnkgaW1tZWRpYXRlbHkgcHJpb3IgZW1lcmdlbmN5IGRlcGFydG1lbnQgdmlzaXQgdG8gdGhlIGRpc2NoYXJnZSBvZiB0aGUgZW5jb3VudGVyKi8NCmRlZmluZSBmdW5jdGlvbiAiSG9zcGl0YWxpemF0aW9uIExlbmd0aCBvZiBTdGF5IihUaGVFbmNvdW50ZXIgRkhJUi5FbmNvdW50ZXIgKToNCiAgTGVuZ3RoSW5EYXlzKCJIb3NwaXRhbGl6YXRpb24iKFRoZUVuY291bnRlcikpDQoNCi8qUmV0dXJucyBhZG1pc3Npb24gdGltZSBmb3IgYW4gZW5jb3VudGVyIG9yIGZvciBpbW1lZGlhdGVseSBwcmlvciBlbWVyZ2VuY3kgZGVwYXJ0bWVudCB2aXNpdC4qLw0KZGVmaW5lIGZ1bmN0aW9uICJIb3NwaXRhbCBBZG1pc3Npb24gVGltZSIoVGhlRW5jb3VudGVyIEZISVIuRW5jb3VudGVyICk6DQogIHN0YXJ0IG9mICJIb3NwaXRhbGl6YXRpb24iKFRoZUVuY291bnRlcikNCg0KLypIb3NwaXRhbCBEaXNjaGFyZ2UgVGltZSByZXR1cm5zIHRoZSBkaXNjaGFyZ2UgdGltZSBmb3IgYW4gZW5jb3VudGVyKi8NCmRlZmluZSBmdW5jdGlvbiAiSG9zcGl0YWwgRGlzY2hhcmdlIFRpbWUiKFRoZUVuY291bnRlciBGSElSLkVuY291bnRlciApOg0KICBlbmQgb2YgRkhJUkhlbHBlcnMuVG9JbnRlcnZhbChUaGVFbmNvdW50ZXIucGVyaW9kKQ0KDQovKlJldHVybnMgZWFybGllc3QgYXJyaXZhbCB0aW1lIGZvciBhbiBlbmNvdW50ZXIgaW5jbHVkaW5nIGFueSBwcmlvciBFRCB2aXNpdC4qLw0KZGVmaW5lIGZ1bmN0aW9uICJIb3NwaXRhbCBBcnJpdmFsIFRpbWUiKFRoZUVuY291bnRlciBGSElSLkVuY291bnRlciApOg0KICBzdGFydCBvZiBGSElSSGVscGVycy5Ub0ludGVydmFsKEZpcnN0KA0KICAJICAgICggIkhvc3BpdGFsaXphdGlvbiBMb2NhdGlvbnMiKFRoZUVuY291bnRlcikgKSBIb3NwaXRhbExvY2F0aW9uDQogIAkJCXNvcnQgYnkgc3RhcnQgb2YgRkhJUkhlbHBlcnMuVG9JbnRlcnZhbChwZXJpb2QpDQogIAkpLnBlcmlvZCkNCiAgDQogIC8vIFRPRE8gLSBmaXggdGhlc2UgKG11c3QgZmV0Y2ggTG9jYXRpb24gcmVzb3VyY2VzIGFuZCBjb21wYXJlIGlkIHRvIHJlZmVyZW5jZSkNCiAgLypSZXR1cm5zIHRoZSBsYXRlc3QgZGVwYXJ0dXJlIHRpbWUgZm9yIGVuY291bnRlciBpbmNsdWRpbmcgYW55IHByaW9yIEVEIHZpc2l0LiAqLw0KICAvKg0KICBkZWZpbmUgZnVuY3Rpb24gIkhvc3BpdGFsIERlcGFydHVyZSBUaW1lIihUaGVFbmNvdW50ZXIgRkhJUi5FbmNvdW50ZXIpOg0KICAJZW5kIG9mIEZISVJIZWxwZXJzLlRvSW50ZXJ2YWwoTGFzdCgNCiAgCSAgICAoICJIb3NwaXRhbGl6YXRpb24gTG9jYXRpb25zIihUaGVFbmNvdW50ZXIpICkgSG9zcGl0YWxMb2NhdGlvbg0KICAJCQlzb3J0IGJ5IHN0YXJ0IG9mIEZISVJIZWxwZXJzLlRvSW50ZXJ2YWwocGVyaW9kKQ0KICAJKS5wZXJpb2QpDQogIA0KICBkZWZpbmUgZnVuY3Rpb24gIkVtZXJnZW5jeSBEZXBhcnRtZW50IEFycml2YWwgVGltZSIoVGhlRW5jb3VudGVyIEZISVIuRW5jb3VudGVyKToNCiAgCXN0YXJ0IG9mIEZISVJIZWxwZXJzLlRvSW50ZXJ2YWwoKA0KICAJICAgIHNpbmdsZXRvbiBmcm9tICgNCiAgCSAgICAgICAgKCAiSG9zcGl0YWxpemF0aW9uIExvY2F0aW9ucyIoVGhlRW5jb3VudGVyKSApIEhvc3BpdGFsTG9jYXRpb24NCiAgCQkJCXdoZXJlIEhvc3BpdGFsTG9jYXRpb24udHlwZSB+ICJFUiINCiAgCQkpDQogIAkpLnBlcmlvZCkNCiAgDQogIGRlZmluZSBmdW5jdGlvbiAiRmlyc3QgSW5wYXRpZW50IEludGVuc2l2ZSBDYXJlIFVuaXQiKFRoZUVuY291bnRlciBGSElSLkVuY291bnRlcik6DQogIAlGaXJzdCgNCiAgCSAgICAoIFRoZUVuY291bnRlci5sb2NhdGlvbiApIEhvc3BpdGFsTG9jYXRpb24NCiAgCQkJd2hlcmUgSG9zcGl0YWxMb2NhdGlvbi50eXBlIH4gIklDVSINCiAgCQkJCWFuZCBIb3NwaXRhbExvY2F0aW9uLnBlcmlvZCBkdXJpbmcgVGhlRW5jb3VudGVyLnBlcmlvZA0KICAJCQlzb3J0IGJ5IHN0YXJ0IG9mIEZISVJIZWxwZXJzLlRvSW50ZXJ2YWwocGVyaW9kKQ0KICAJKQ0KICAqLw0KICANCiAgLypIb3NwaXRhbGl6YXRpb24gd2l0aCBPYnNlcnZhdGlvbiBhbmQgT3V0cGF0aWVudCBTdXJnZXJ5IFNlcnZpY2UgcmV0dXJucyB0aGUgdG90YWwgaW50ZXJ2YWwgZnJvbSB0aGUgc3RhcnQgb2YgYW55IGltbWVkaWF0ZWx5IHByaW9yIGVtZXJnZW5jeSBkZXBhcnRtZW50IHZpc2l0LCBvdXRwYXRpZW50IHN1cmdlcnkgdmlzaXQgb3Igb2JzZXJ2YXRpb24gdmlzaXQgdG8gdGhlIGRpc2NoYXJnZSBvZiB0aGUgZ2l2ZW4gZW5jb3VudGVyLiovDQogIC8qIFRPRE86DQogIGRlZmluZSBmdW5jdGlvbiAiSG9zcGl0YWxpemF0aW9uV2l0aE9ic2VydmF0aW9uQW5kT3V0cGF0aWVudFN1cmdlcnlTZXJ2aWNlIihFbmNvdW50ZXIgIkVuY291bnRlciwgUGVyZm9ybWVkIiApOg0KICBFbmNvdW50ZXIgVmlzaXQNCiAgCWxldCBPYnNWaXNpdDogTGFzdChbIkVuY291bnRlciwgUGVyZm9ybWVkIjogIk9ic2VydmF0aW9uIFNlcnZpY2VzIl0gTGFzdE9icw0KICAJCQl3aGVyZSBMYXN0T2JzLnJlbGV2YW50UGVyaW9kIGVuZHMgMSBob3VyIG9yIGxlc3Mgb24gb3IgYmVmb3JlIHN0YXJ0IG9mIFZpc2l0LnJlbGV2YW50UGVyaW9kDQogIAkJCXNvcnQgYnkNCiAgCQkJZW5kIG9mIHJlbGV2YW50UGVyaW9kDQogIAkpLA0KICAJVmlzaXRTdGFydDogQ29hbGVzY2Uoc3RhcnQgb2YgT2JzVmlzaXQucmVsZXZhbnRQZXJpb2QsIHN0YXJ0IG9mIFZpc2l0LnJlbGV2YW50UGVyaW9kKSwNCiAgCUVEVmlzaXQ6IExhc3QoWyJFbmNvdW50ZXIsIFBlcmZvcm1lZCI6ICJFbWVyZ2VuY3kgRGVwYXJ0bWVudCBWaXNpdCJdIExhc3RFRA0KICAJCQl3aGVyZSBMYXN0RUQucmVsZXZhbnRQZXJpb2QgZW5kcyAxIGhvdXIgb3IgbGVzcyBvbiBvciBiZWZvcmUgVmlzaXRTdGFydA0KICAJCQlzb3J0IGJ5DQogIAkJCWVuZCBvZiByZWxldmFudFBlcmlvZA0KICAJKSwNCiAgCVZpc2l0U3RhcnRXaXRoRUQ6IENvYWxlc2NlKHN0YXJ0IG9mIEVEVmlzaXQucmVsZXZhbnRQZXJpb2QsIFZpc2l0U3RhcnQpLA0KICAJT3V0cGF0aWVudFN1cmdlcnlWaXNpdDogTGFzdChbIkVuY291bnRlciwgUGVyZm9ybWVkIjogIk91dHBhdGllbnQgU3VyZ2VyeSBTZXJ2aWNlIl0gTGFzdFN1cmdlcnlPUA0KICAJCQl3aGVyZSBMYXN0U3VyZ2VyeU9QLnJlbGV2YW50UGVyaW9kIGVuZHMgMSBob3VyIG9yIGxlc3Mgb24gb3IgYmVmb3JlIFZpc2l0U3RhcnRXaXRoRUQNCiAgCQkJc29ydCBieQ0KICAJCQllbmQgb2YgcmVsZXZhbnRQZXJpb2QNCiAgCSkNCiAgCXJldHVybiBJbnRlcnZhbFtDb2FsZXNjZShzdGFydCBvZiBPdXRwYXRpZW50U3VyZ2VyeVZpc2l0LnJlbGV2YW50UGVyaW9kLCBWaXNpdFN0YXJ0V2l0aEVEKSwNCiAgCWVuZCBvZiBWaXNpdC5yZWxldmFudFBlcmlvZF0NCiAgKi8NCg0KLypIb3NwaXRhbGl6YXRpb24gd2l0aCBPYnNlcnZhdGlvbiByZXR1cm5zIHRoZSB0b3RhbCBpbnRlcnZhbCBmcm9tIHRoZSBzdGFydCBvZiBhbnkgaW1tZWRpYXRlbHkgcHJpb3IgZW1lcmdlbmN5IGRlcGFydG1lbnQgdmlzaXQgdGhyb3VnaCB0aGUgb2JzZXJ2YXRpb24gdmlzaXQgdG8gdGhlIGRpc2NoYXJnZSBvZiB0aGUgZ2l2ZW4gZW5jb3VudGVyKi8NCmRlZmluZSBmdW5jdGlvbiAiSG9zcGl0YWxpemF0aW9uV2l0aE9ic2VydmF0aW9uIihUaGVFbmNvdW50ZXIgRkhJUi5FbmNvdW50ZXIgKToNCiAgVGhlRW5jb3VudGVyIFZpc2l0DQogIAkJbGV0IE9ic1Zpc2l0OiBMYXN0KFtFbmNvdW50ZXI6ICJPYnNlcnZhdGlvbiBTZXJ2aWNlcyJdIExhc3RPYnMNCiAgCQkJCXdoZXJlIExhc3RPYnMucGVyaW9kIGVuZHMgMSBob3VyIG9yIGxlc3Mgb24gb3IgYmVmb3JlIHN0YXJ0IG9mIFZpc2l0LnBlcmlvZA0KICAJCQkJc29ydCBieSBlbmQgb2YgcGVyaW9kDQogIAkJCSksDQogIAkJCVZpc2l0U3RhcnQ6IENvYWxlc2NlKHN0YXJ0IG9mIE9ic1Zpc2l0LnBlcmlvZCwgc3RhcnQgb2YgVmlzaXQucGVyaW9kKSwNCiAgCQkJRURWaXNpdDogTGFzdChbRW5jb3VudGVyOiAiRW1lcmdlbmN5IERlcGFydG1lbnQgVmlzaXQiXSBMYXN0RUQNCiAgCQkJCXdoZXJlIExhc3RFRC5wZXJpb2QgZW5kcyAxIGhvdXIgb3IgbGVzcyBvbiBvciBiZWZvcmUgVmlzaXRTdGFydA0KICAJCQkJc29ydCBieSBlbmQgb2YgcGVyaW9kDQogIAkJCSkNCiAgCQlyZXR1cm4gSW50ZXJ2YWxbQ29hbGVzY2Uoc3RhcnQgb2YgRURWaXNpdC5wZXJpb2QsIFZpc2l0U3RhcnQpLCBlbmQgb2YgVmlzaXQucGVyaW9kXQ0KDQovKioNCiogTm9ybWFsaXplcyB0aGUgaW5wdXQgYXJndW1lbnQgdG8gYW4gaW50ZXJ2YWwgcmVwcmVzZW50YXRpb24uDQoqIFRoZSBpbnB1dCBjYW4gYmUgcHJvdmlkZWQgYXMgYSBkYXRlVGltZSwgUGVyaW9kLCBUaW1pbmcsIGluc3RhbnQsIHN0cmluZywgQWdlLCBvciBSYW5nZS4NCiogVGhlIGludGVudCBvZiB0aGlzIGZ1bmN0aW9uIGlzIHRvIHByb3ZpZGUgYSBjbGVhciBhbmQgY29uY2lzZSBtZWNoYW5pc20gdG8gdHJlYXQgc2luZ2xlDQoqIGVsZW1lbnRzIHRoYXQgaGF2ZSBtdWx0aXBsZSBwb3NzaWJsZSByZXByZXNlbnRhdGlvbnMgYXMgaW50ZXJ2YWxzIHNvIHRoYXQgbG9naWMgZG9lc24ndCBoYXZlIHRvIGFjY291bnQNCiogZm9yIHRoZSB2YXJpYWJpbGl0eS4gTW9yZSBjb21wbGV4IGNhbGN1bGF0aW9ucyAoc3VjaCBhcyBtZWRpY2F0aW9uIHJlcXVlc3QgcGVyaW9kIG9yIGRpc3BlbnNlIHBlcmlvZA0KKiBjYWxjdWxhdGlvbikgbmVlZCBzcGVjaWZpYyBndWlkYW5jZSBhbmQgY29uc2lkZXJhdGlvbi4gVGhhdCBndWlkYW5jZSBtYXkgbWFrZSB1c2Ugb2YgdGhpcyBmdW5jdGlvbiwgYnV0DQoqIHRoZSBmb2N1cyBvZiB0aGlzIGZ1bmN0aW9uIGlzIG9uIHNpbmdsZSBlbGVtZW50IGNhbGN1bGF0aW9ucyB3aGVyZSB0aGUgc2VtYW50aWNzIGFyZSB1bmFtYmlndW91cy4NCiogSWYgdGhlIGlucHV0IGlzIGEgZGF0ZVRpbWUsIHRoZSByZXN1bHQgYSBEYXRlVGltZSBJbnRlcnZhbCBiZWdpbm5pbmcgYW5kIGVuZGluZyBvbiB0aGF0IGRhdGVUaW1lLg0KKiBJZiB0aGUgaW5wdXQgaXMgYSBQZXJpb2QsIHRoZSByZXN1bHQgaXMgYSBEYXRlVGltZSBJbnRlcnZhbC4NCiogSWYgdGhlIGlucHV0IGlzIGEgVGltaW5nLCBhbiBlcnJvciBpcyByYWlzZWQgaW5kaWNhdGluZyBhIHNpbmdsZSBpbnRlcnZhbCBjYW5ub3QgYmUgY29tcHV0ZWQgZnJvbSBhIFRpbWluZy4NCiogSWYgdGhlIGlucHV0IGlzIGFuIGluc3RhbnQsIHRoZSByZXN1bHQgaXMgYSBEYXRlVGltZSBJbnRlcnZhbCBiZWdpbm5pbmcgYW5kIGVuZGluZyBvbiB0aGF0IGluc3RhbnQuDQoqIElmIHRoZSBpbnB1dCBpcyBhIHN0cmluZywgYW4gZXJyb3IgaXMgcmFpc2VkIGluZGljYXRpbmcgYSBzaW5nbGUgaW50ZXJ2YWwgY2Fubm90IGJlIGNvbXB1dGVkIGZyb20gYSBzdHJpbmcuDQoqIElmIHRoZSBpbnB1dCBpcyBhbiBBZ2UsIHRoZSByZXN1bHQgaXMgYSBEYXRlVGltZSBJbnRlcnZhbCBiZWdpbm5pbmcgd2hlbiB0aGUgcGF0aWVudCB3YXMgdGhlIGdpdmVuIEFnZSwNCmFuZCBlbmRpbmcgaW1tZWRpYXRlbHkgcHJpb3IgdG8gd2hlbiB0aGUgcGF0aWVudCB3YXMgdGhlIGdpdmVuIEFnZSBwbHVzIG9uZSB5ZWFyLg0KKiBJZiB0aGUgaW5wdXQgaXMgYSBSYW5nZSwgdGhlIHJlc3VsdCBpcyBhIERhdGVUaW1lIEludGVydmFsIGJlZ2lubmluZyB3aGVuIHRoZSBwYXRpZW50IHdhcyB0aGUgQWdlIGdpdmVuDQpieSB0aGUgbG93IGVuZCBvZiB0aGUgUmFuZ2UsIGFuZCBlbmRpbmcgaW1tZWRpYXRlbHkgcHJpb3IgdG8gd2hlbiB0aGUgcGF0aWVudCB3YXMgdGhlIEFnZSBnaXZlbiBieSB0aGUNCmhpZ2ggZW5kIG9mIHRoZSBSYW5nZSBwbHVzIG9uZSB5ZWFyLiovDQpkZWZpbmUgZnVuY3Rpb24gIk5vcm1hbGl6ZSBJbnRlcnZhbCIoY2hvaWNlIENob2ljZTxGSElSLmRhdGVUaW1lLCBGSElSLlBlcmlvZCwgRkhJUi5UaW1pbmcsIEZISVIuaW5zdGFudCwgRkhJUi5zdHJpbmcsIEZISVIuQWdlLCBGSElSLlJhbmdlPiApOg0KICBjYXNlDQogIAkgIHdoZW4gY2hvaWNlIGlzIEZISVIuZGF0ZVRpbWUgdGhlbg0KICAJSW50ZXJ2YWxbRkhJUkhlbHBlcnMuVG9EYXRlVGltZShjaG9pY2UgYXMgRkhJUi5kYXRlVGltZSksIEZISVJIZWxwZXJzLlRvRGF0ZVRpbWUoY2hvaWNlIGFzIEZISVIuZGF0ZVRpbWUpXQ0KICAJCXdoZW4gY2hvaWNlIGlzIEZISVIuUGVyaW9kIHRoZW4NCiAgCQlGSElSSGVscGVycy5Ub0ludGVydmFsKGNob2ljZSBhcyBGSElSLlBlcmlvZCkNCiAgCQl3aGVuIGNob2ljZSBpcyBGSElSLmluc3RhbnQgdGhlbg0KICAJCQlJbnRlcnZhbFtGSElSSGVscGVycy5Ub0RhdGVUaW1lKGNob2ljZSBhcyBGSElSLmluc3RhbnQpLCBGSElSSGVscGVycy5Ub0RhdGVUaW1lKGNob2ljZSBhcyBGSElSLmluc3RhbnQpXQ0KICAJCXdoZW4gY2hvaWNlIGlzIEZISVIuQWdlIHRoZW4NCiAgCQkgIEludGVydmFsW0ZISVJIZWxwZXJzLlRvRGF0ZShQYXRpZW50LmJpcnRoRGF0ZSkgKyBGSElSSGVscGVycy5Ub1F1YW50aXR5KGNob2ljZSBhcyBGSElSLkFnZSksDQogIAkJCSAgRkhJUkhlbHBlcnMuVG9EYXRlKFBhdGllbnQuYmlydGhEYXRlKSArIEZISVJIZWxwZXJzLlRvUXVhbnRpdHkoY2hvaWNlIGFzIEZISVIuQWdlKSArIDEgeWVhcikNCiAgCQl3aGVuIGNob2ljZSBpcyBGSElSLlJhbmdlIHRoZW4NCiAgCQkgIEludGVydmFsW0ZISVJIZWxwZXJzLlRvRGF0ZShQYXRpZW50LmJpcnRoRGF0ZSkgKyBGSElSSGVscGVycy5Ub1F1YW50aXR5KChjaG9pY2UgYXMgRkhJUi5SYW5nZSkubG93KSwNCiAgCQkJICBGSElSSGVscGVycy5Ub0RhdGUoUGF0aWVudC5iaXJ0aERhdGUpICsgRkhJUkhlbHBlcnMuVG9RdWFudGl0eSgoY2hvaWNlIGFzIEZISVIuUmFuZ2UpLmhpZ2gpICsgMSB5ZWFyKQ0KICAJCXdoZW4gY2hvaWNlIGlzIEZISVIuVGltaW5nIHRoZW4NCiAgCQkgIE1lc3NhZ2UobnVsbCBhcyBJbnRlcnZhbDxEYXRlVGltZT4sIHRydWUsICcxJywgJ0Vycm9yJywgJ0Nhbm5vdCBjb21wdXRlIGEgc2luZ2xlIGludGVydmFsIGZyb20gYSBUaW1pbmcgdHlwZScpDQogICAgd2hlbiBjaG9pY2UgaXMgRkhJUi5zdHJpbmcgdGhlbg0KICAgICAgTWVzc2FnZShudWxsIGFzIEludGVydmFsPERhdGVUaW1lPiwgdHJ1ZSwgJzEnLCAnRXJyb3InLCAnQ2Fubm90IGNvbXB1dGUgYW4gaW50ZXJ2YWwgZnJvbSBhIFN0cmluZyB2YWx1ZScpDQogIAkJZWxzZQ0KICAJCQludWxsIGFzIEludGVydmFsPERhdGVUaW1lPg0KICAJZW5kDQoNCi8qKg0KKiBSZXR1cm5zIGFuIGludGVydmFsIHJlcHJlc2VudGluZyB0aGUgYWJhdGVtZW50IG9mIHRoZSBnaXZlbiBjb25kaXRpb24sIGlmIGFuDQphYmF0ZW1lbnQgZWxlbWVudCBpcyBwcmVzZW50LCBudWxsIG90aGVyd2lzZS4NClRoaXMgZnVuY3Rpb24gdXNlcyB0aGUgc2VtYW50aWNzIG9mIE5vcm1hbGl6ZSBJbnRlcnZhbCB0byBpbnRlcnByZXQgdGhlIGFiYXRlbWVudA0KZWxlbWVudC4qLw0KZGVmaW5lIGZ1bmN0aW9uICJOb3JtYWxpemUgQWJhdGVtZW50Iihjb25kaXRpb24gQ29uZGl0aW9uICk6DQogIGlmIGNvbmRpdGlvbi5hYmF0ZW1lbnQgaXMgRkhJUi5kYXRlVGltZSB0aGVuDQogIAkgIEludGVydmFsW0ZISVJIZWxwZXJzLlRvRGF0ZVRpbWUoY29uZGl0aW9uLmFiYXRlbWVudCBhcyBGSElSLmRhdGVUaW1lKSwgRkhJUkhlbHBlcnMuVG9EYXRlVGltZShjb25kaXRpb24uYWJhdGVtZW50IGFzIEZISVIuZGF0ZVRpbWUpXQ0KICAJZWxzZSBpZiBjb25kaXRpb24uYWJhdGVtZW50IGlzIEZISVIuUGVyaW9kIHRoZW4NCiAgCSAgRkhJUkhlbHBlcnMuVG9JbnRlcnZhbChjb25kaXRpb24uYWJhdGVtZW50IGFzIEZISVIuUGVyaW9kKQ0KICAJZWxzZSBpZiBjb25kaXRpb24uYWJhdGVtZW50IGlzIEZISVIuc3RyaW5nIHRoZW4NCiAgTWVzc2FnZShudWxsIGFzIEludGVydmFsPERhdGVUaW1lPiwgdHJ1ZSwgJzEnLCAnRXJyb3InLCAnQ2Fubm90IGNvbXB1dGUgYW4gaW50ZXJ2YWwgZnJvbSBhIFN0cmluZyB2YWx1ZScpDQogIAllbHNlIGlmIGNvbmRpdGlvbi5hYmF0ZW1lbnQgaXMgRkhJUi5BZ2UgdGhlbg0KICAJCUludGVydmFsW0ZISVJIZWxwZXJzLlRvRGF0ZShQYXRpZW50LmJpcnRoRGF0ZSkgKyBGSElSSGVscGVycy5Ub1F1YW50aXR5KGNvbmRpdGlvbi5hYmF0ZW1lbnQgYXMgRkhJUi5BZ2UpLA0KICAJCQlGSElSSGVscGVycy5Ub0RhdGUoUGF0aWVudC5iaXJ0aERhdGUpICsgRkhJUkhlbHBlcnMuVG9RdWFudGl0eShjb25kaXRpb24uYWJhdGVtZW50IGFzIEZISVIuQWdlKSArIDEgeWVhcikNCiAgCWVsc2UgaWYgY29uZGl0aW9uLmFiYXRlbWVudCBpcyBGSElSLlJhbmdlIHRoZW4NCiAgCSAgSW50ZXJ2YWxbRkhJUkhlbHBlcnMuVG9EYXRlKFBhdGllbnQuYmlydGhEYXRlKSArIEZISVJIZWxwZXJzLlRvUXVhbnRpdHkoKGNvbmRpdGlvbi5hYmF0ZW1lbnQgYXMgRkhJUi5SYW5nZSkubG93KSwNCiAgCQkgIEZISVJIZWxwZXJzLlRvRGF0ZShQYXRpZW50LmJpcnRoRGF0ZSkgKyBGSElSSGVscGVycy5Ub1F1YW50aXR5KChjb25kaXRpb24uYWJhdGVtZW50IGFzIEZISVIuUmFuZ2UpLmhpZ2gpICsgMSB5ZWFyKQ0KICAJZWxzZSBpZiBjb25kaXRpb24uYWJhdGVtZW50IGlzIEZISVIuYm9vbGVhbiB0aGVuDQogIAkgIEludGVydmFsW2VuZCBvZiAiTm9ybWFsaXplIEludGVydmFsIihjb25kaXRpb24ub25zZXQpLCBjb25kaXRpb24ucmVjb3JkZWREYXRlKQ0KICAJZWxzZSBudWxsDQoNCi8qUmV0dXJucyBhbiBpbnRlcnZhbCByZXByZXNlbnRpbmcgdGhlIHBlcmlvZCBkdXJpbmcgd2hpY2ggdGhlIGNvbmRpdGlvbiB3YXMgcHJldmFsZW50IChpLmUuIG9uc2V0IHRvIGFiYXRlbWVudCkNCklmIHRoZSBjb25kaXRpb24gaXMgImFjdGl2ZSIsIHRoZW4gYWJhdGVtZW50IGJlaW5nIHVua25vd24NCndvdWxkIGluZGljYXRlIHRoZSBjb25kaXRpb24gaXMgb25nb2luZywgYW5kIHRoZSBlbmRpbmcgYm91bmRhcnkgb2YgdGhlIHByZXZhbGVuY2UNCnBlcmlvZCBpcyBpbmNsdXNpdmUsIG90aGVyd2lzZSwgdGhlIGFiYXRlbWVudCBpcyBjb25zaWRlcmVkIHVua25vd24gYW5kIHRoZSBlbmRpbmcgYm91bmRhcnkNCm9mIHRoZSBwcmV2YWxlbmNlIHBlcmlvZCBpcyBleGNsdXNpdmUuDQpOb3RlIHRoYXQgd2hlbiB1c2luZyB0aGlzIGZ1bmN0aW9uIGl0IHNob3VsZCBiZSBub3RlZCB0aGF0IG1hbnkgY2xpbmljYWwgc3lzdGVtcw0KZG8gbm90IGFjdHVhbGx5IGNhcHR1cmUgYWJhdGVtZW50LCBzbyBjYXJlIHNob3VsZCBiZSB0YWtlbiB3aGVuIHVzaW5nIHRoaXMgZnVuY3Rpb24NCnRvIG1lZXQgY2xpbmljYWwgaW50ZW50LiovDQpkZWZpbmUgZnVuY3Rpb24gIlByZXZhbGVuY2UgUGVyaW9kIihjb25kaXRpb24gQ29uZGl0aW9uICk6DQogIGlmIGNvbmRpdGlvbi5jbGluaWNhbFN0YXR1cyB+ICJhY3RpdmUiDQogICAgb3IgY29uZGl0aW9uLmNsaW5pY2FsU3RhdHVzIH4gInJlY3VycmVuY2UiDQogICAgb3IgY29uZGl0aW9uLmNsaW5pY2FsU3RhdHVzIH4gInJlbGFwc2UiIHRoZW4NCiAgICBJbnRlcnZhbFtzdGFydCBvZiAiTm9ybWFsaXplIEludGVydmFsIihjb25kaXRpb24ub25zZXQpLCBlbmQgb2YgIk5vcm1hbGl6ZSBBYmF0ZW1lbnQiKGNvbmRpdGlvbildDQogIGVsc2UNCiAgICBJbnRlcnZhbFtzdGFydCBvZiAiTm9ybWFsaXplIEludGVydmFsIihjb25kaXRpb24ub25zZXQpLCBlbmQgb2YgIk5vcm1hbGl6ZSBBYmF0ZW1lbnQiKGNvbmRpdGlvbikpDQoNCi8qUmV0dXJucyB0aGUgdGFpbCBvZiB0aGUgZ2l2ZW4gdXJpIChpLmUuIGV2ZXJ5dGhpbmcgYWZ0ZXIgdGhlIGxhc3Qgc2xhc2ggaW4gdGhlIFVSSSkuKi8NCmRlZmluZSBmdW5jdGlvbiAiR2V0SWQiKHVyaSBTdHJpbmcgKToNCiAgTGFzdChTcGxpdCh1cmksICcvJykpDQoNCi8qUmV0dXJucyB0aGUgQ29uZGl0aW9uIHJlc291cmNlcyByZWZlcmVuY2VkIGJ5IHRoZSBkaWFnbm9zaXMgZWxlbWVudCBvZiB0aGUgRW5jb3VudGVyKi8NCmRlZmluZSBmdW5jdGlvbiAiRW5jb3VudGVyRGlhZ25vc2lzIihFbmNvdW50ZXIgRW5jb3VudGVyICk6DQogIEVuY291bnRlci5kaWFnbm9zaXMgRA0KICAgIHJldHVybiBzaW5nbGV0b24gZnJvbSAoW0NvbmRpdGlvbl0gQ29uZGl0aW9ucyB3aGVyZSBDb25kaXRpb25zLmlkID0gR2V0SWQoRC5jb25kaXRpb24ucmVmZXJlbmNlKSkNCiAgDQogIC8vIFJldHVybnMgdGhlIGNvbmRpdGlvbiB0aGF0IGlzIHNwZWNpZmllZCBhcyB0aGUgcHJpbmNpcGFsIGRpYWdub3NpcyBmb3IgdGhlIGVuY291bnRlcg0KICAvLyBUT0RPOiBCVFIgMjAxOS0wNy0zMDogU2hvdWxkbid0IG5lZWQgdGhlIEZISVJIZWxwZXJzIHJlZmVyZW5jZSBoZXJlLCBpbnZlc3RpZ2F0ZQ0KDQpkZWZpbmUgZnVuY3Rpb24gIlByaW5jaXBhbERpYWdub3NpcyIoRW5jb3VudGVyIEVuY291bnRlciApOg0KICAoc2luZ2xldG9uIGZyb20gKEVuY291bnRlci5kaWFnbm9zaXMgRCB3aGVyZSBGSElSSGVscGVycy5Ub0ludGVnZXIoRC5yYW5rKSA9IDEpKSBQRA0KCXJldHVybiBzaW5nbGV0b24gZnJvbSAoW0NvbmRpdGlvbl0gQ29uZGl0aW9ucyB3aGVyZSBDb25kaXRpb25zLmlkID0gR2V0SWQoUEQuY29uZGl0aW9uLnJlZmVyZW5jZSkpDQogIC8vIFJldHVybnMgdGhlIGxvY2F0aW9uIGZvciB0aGUgZ2l2ZW4gbG9jYXRpb24gcmVmZXJlbmNlDQoNCi8qUmV0dXJucyB0aGUgTG9jYXRpb24gcmVzb3VyY2Ugc3BlY2lmaWVkIGJ5IHRoZSBnaXZlbiByZWZlcmVuY2UqLw0KZGVmaW5lIGZ1bmN0aW9uICJHZXRMb2NhdGlvbiIocmVmZXJlbmNlIFJlZmVyZW5jZSApOg0KICBzaW5nbGV0b24gZnJvbSAoDQoJW0xvY2F0aW9uXSBMb2NhdGlvbnMNCgl3aGVyZSBMb2NhdGlvbnMuaWQgPSBHZXRJZChyZWZlcmVuY2UucmVmZXJlbmNlKQ0KICApDQoNCi8qTk9URTogRXh0ZW5zaW9ucyBhcmUgbm90IHRoZSBwcmVmZXJyZWQgYXBwcm9hY2gsIGJ1dCBhcmUgdXNlZCBhcyBhIHdheSB0byBhY2Nlc3MNCmNvbnRlbnQgdGhhdCBpcyBkZWZpbmVkIGJ5IGV4dGVuc2lvbnMgYnV0IG5vdCB5ZXQgc3VyZmFjZWQgaW4gdGhlDQpDUUwgbW9kZWwgaW5mby4qLw0KZGVmaW5lIGZ1bmN0aW9uICJHZXRFeHRlbnNpb25zIihkb21haW5SZXNvdXJjZSBEb21haW5SZXNvdXJjZSwgdXJsIFN0cmluZyApOg0KICBkb21haW5SZXNvdXJjZS5leHRlbnNpb24gRQ0KICAJICB3aGVyZSBFLnVybCA9ICgnaHR0cDovL2hsNy5vcmcvZmhpci91cy9xaWNvcmUvU3RydWN0dXJlRGVmaW5pdGlvbi8nICsgdXJsKQ0KICAJCXJldHVybiBFDQoNCmRlZmluZSBmdW5jdGlvbiAiR2V0RXh0ZW5zaW9uIihkb21haW5SZXNvdXJjZSBEb21haW5SZXNvdXJjZSwgdXJsIFN0cmluZyApOg0KICBzaW5nbGV0b24gZnJvbSAiR2V0RXh0ZW5zaW9ucyIoZG9tYWluUmVzb3VyY2UsIHVybCkNCg0KLypOT1RFOiBFeHRlbnNpb25zIGFyZSBub3QgdGhlIHByZWZlcnJlZCBhcHByb2FjaCwgYnV0IGFyZSB1c2VkIGFzIGEgd2F5IHRvIGFjY2Vzcw0KY29udGVudCB0aGF0IGlzIGRlZmluZWQgYnkgZXh0ZW5zaW9ucyBidXQgbm90IHlldCBzdXJmYWNlZCBpbiB0aGUNCkNRTCBtb2RlbCBpbmZvLiovDQpkZWZpbmUgZnVuY3Rpb24gIkdldEV4dGVuc2lvbnMiKGVsZW1lbnQgRWxlbWVudCwgdXJsIFN0cmluZyApOg0KICBlbGVtZW50LmV4dGVuc2lvbiBFDQogIAkgIHdoZXJlIEUudXJsID0gKHVybCkNCiAgCQlyZXR1cm4gRQ0KDQpkZWZpbmUgZnVuY3Rpb24gIkdldEV4dGVuc2lvbiIoZWxlbWVudCBFbGVtZW50LCB1cmwgU3RyaW5nICk6DQogIHNpbmdsZXRvbiBmcm9tICJHZXRFeHRlbnNpb25zIihlbGVtZW50LCB1cmwpDQoNCi8qTk9URTogRXh0ZW5zaW9ucyBhcmUgbm90IHRoZSBwcmVmZXJyZWQgYXBwcm9hY2gsIGJ1dCBhcmUgdXNlZCBhcyBhIHdheSB0byBhY2Nlc3MNCmNvbnRlbnQgdGhhdCBpcyBkZWZpbmVkIGJ5IGV4dGVuc2lvbnMgYnV0IG5vdCB5ZXQgc3VyZmFjZWQgaW4gdGhlDQpDUUwgbW9kZWwgaW5mby4qLw0KZGVmaW5lIGZ1bmN0aW9uICJHZXRCYXNlRXh0ZW5zaW9ucyIoZG9tYWluUmVzb3VyY2UgRG9tYWluUmVzb3VyY2UsIHVybCBTdHJpbmcgKToNCiAgZG9tYWluUmVzb3VyY2UuZXh0ZW5zaW9uIEUNCiAgCSAgd2hlcmUgRS51cmwgPSAoJ2h0dHA6Ly9obDcub3JnL2ZoaXIvU3RydWN0dXJlRGVmaW5pdGlvbi8nICsgdXJsKQ0KICAJCXJldHVybiBFDQoNCmRlZmluZSBmdW5jdGlvbiAiR2V0QmFzZUV4dGVuc2lvbiIoZG9tYWluUmVzb3VyY2UgRG9tYWluUmVzb3VyY2UsIHVybCBTdHJpbmcgKToNCiAgc2luZ2xldG9uIGZyb20gIkdldEJhc2VFeHRlbnNpb25zIihkb21haW5SZXNvdXJjZSwgdXJsKQ0KDQovKkBkZXNjcmlwdGlvbjogUmV0dXJucyBhbnkgYmFzZS1GSElSIGV4dGVuc2lvbnMgZGVmaW5lZCBvbiB0aGUgZ2l2ZW4gZWxlbWVudCB3aXRoIHRoZSBzcGVjaWZpZWQgaWQuDQpAY29tbWVudDogTk9URTogRXh0ZW5zaW9ucyBhcmUgbm90IHRoZSBwcmVmZXJyZWQgYXBwcm9hY2gsIGJ1dCBhcmUgdXNlZCBhcyBhIHdheSB0byBhY2Nlc3MNCmNvbnRlbnQgdGhhdCBpcyBkZWZpbmVkIGJ5IGV4dGVuc2lvbnMgYnV0IG5vdCB5ZXQgc3VyZmFjZWQgaW4gdGhlIENRTCBtb2RlbCBpbmZvLiovDQpkZWZpbmUgZnVuY3Rpb24gIkJhc2VFeHRlbnNpb25zIihlbGVtZW50IEVsZW1lbnQsIGlkIFN0cmluZyApOg0KICBlbGVtZW50LmV4dGVuc2lvbiBFDQogIAkgIHdoZXJlIEUudXJsID0gKCdodHRwOi8vaGw3Lm9yZy9maGlyL1N0cnVjdHVyZURlZmluaXRpb24vJyArIGlkKQ0KICAJCXJldHVybiBFDQoNCi8qQGRlc2NyaXB0aW9uOiBSZXR1cm5zIHRoZSBzaW5nbGUgYmFzZS1GSElSIGV4dGVuc2lvbiAoaWYgcHJlc2VudCkgb24gdGhlIGdpdmVuIGVsZW1lbnQgd2l0aCB0aGUgc3BlY2lmaWVkIGlkLg0KQGNvbW1lbnQ6IFRoaXMgZnVuY3Rpb24gdXNlcyBzaW5nbGV0b24gZnJvbSB0byBlbnN1cmUgdGhhdCBhIHJ1bi10aW1lIGV4Y2VwdGlvbiBpcyB0aHJvd24gaWYgdGhlcmUNCmlzIG1vcmUgdGhhbiBvbmUgZXh0ZW5zaW9uIG9uIHRoZSBnaXZlbiByZXNvdXJjZSB3aXRoIHRoZSBzcGVjaWZpZWQgdXJsLiovDQpkZWZpbmUgZnVuY3Rpb24gIkJhc2VFeHRlbnNpb24iKGVsZW1lbnQgRWxlbWVudCwgaWQgU3RyaW5nICk6DQogIHNpbmdsZXRvbiBmcm9tIEJhc2VFeHRlbnNpb25zKGVsZW1lbnQsIGlkKQ0KDQovKk5PVEU6IFByb3ZlbmFuY2UgaXMgbm90IHRoZSBwcmVmZXJyZWQgYXBwcm9hY2gsIHRoaXMgaXMgcHJvdmlkZWQgb25seSBhcyBhbiBpbGx1c3RyYXRpb24NCmZvciB3aGF0IHVzaW5nIFByb3ZlbmFuY2UgY291bGQgbG9vayBsaWtlLCBhbmQgaXMgbm90IGEgdGVzdGVkIHBhdHRlcm4qLw0KZGVmaW5lIGZ1bmN0aW9uICJHZXRQcm92ZW5hbmNlIihyZXNvdXJjZSBSZXNvdXJjZSApOg0KICBzaW5nbGV0b24gZnJvbSAoW1Byb3ZlbmFuY2U6IHRhcmdldCBpbiByZXNvdXJjZS5pZF0pDQoNCmRlZmluZSBmdW5jdGlvbiAiR2V0TWVkaWNhdGlvbkNvZGUiKHJlcXVlc3QgTWVkaWNhdGlvblJlcXVlc3QgKToNCiAgaWYgcmVxdWVzdC5tZWRpY2F0aW9uIGlzIENvZGVhYmxlQ29uY2VwdCB0aGVuDQogIAkgIHJlcXVlc3QubWVkaWNhdGlvbiBhcyBDb2RlYWJsZUNvbmNlcHQNCiAgCWVsc2UNCgkJKHNpbmdsZXRvbiBmcm9tIChbTWVkaWNhdGlvbl0gTWVkaWNhdGlvbnMgd2hlcmUgTWVkaWNhdGlvbnMuaWQgPSBHZXRJZCgocmVxdWVzdC5tZWRpY2F0aW9uIGFzIFJlZmVyZW5jZSkucmVmZXJlbmNlKSkpLmNvZGUNCg0KLypHaXZlbiBhbiBpbnRlcnZhbCwgcmV0dXJuIHRydWUgaWYgdGhlIGludGVydmFsIGhhcyBhIHN0YXJ0aW5nIGJvdW5kYXJ5IHNwZWNpZmllZCAoaS5lLiB0aGUgc3RhcnQgb2YgdGhlIGludGVydmFsIGlzIG5vdCBudWxsIGFuZCBub3QgdGhlIG1pbmltdW0gRGF0ZVRpbWUgdmFsdWUpKi8NCmRlZmluZSBmdW5jdGlvbiAiSGFzU3RhcnQiKHBlcmlvZCBJbnRlcnZhbDxEYXRlVGltZT4gKToNCiAgbm90ICggc3RhcnQgb2YgcGVyaW9kIGlzIG51bGwNCiAgICAgIG9yIHN0YXJ0IG9mIHBlcmlvZCA9IG1pbmltdW0gRGF0ZVRpbWUNCiAgKQ0KDQovKkdpdmVuIGFuIGludGVydmFsLCByZXR1cm4gdHJ1ZSBpZiB0aGUgaW50ZXJ2YWwgaGFzIGFuIGVuZGluZyBib3VuZGFyeSBzcGVjaWZpZWQgKGkuZS4gdGhlIGVuZCBvZiB0aGUgaW50ZXJ2YWwgaXMgbm90IG51bGwgYW5kIG5vdCB0aGUgbWF4aW11bSBEYXRlVGltZSB2YWx1ZSkqLw0KZGVmaW5lIGZ1bmN0aW9uICJIYXNFbmQiKHBlcmlvZCBJbnRlcnZhbDxEYXRlVGltZT4gKToNCiAgbm90ICgNCiAgICBlbmQgb2YgcGVyaW9kIGlzIG51bGwNCiAgICAgIG9yDQogICAgICBlbmQgb2YgcGVyaW9kID0gbWF4aW11bSBEYXRlVGltZQ0KICApDQoNCi8qR2l2ZW4gYW4gaW50ZXJ2YWwsIHJldHVybiB0aGUgZW5kaW5nIHBvaW50IGlmIHRoZSBpbnRlcnZhbCBoYXMgYW4gZW5kaW5nIGJvdW5kYXJ5IHNwZWNpZmllZCwgb3RoZXJ3aXNlLCByZXR1cm4gdGhlIHN0YXJ0aW5nIHBvaW50Ki8NCmRlZmluZSBmdW5jdGlvbiAiTGF0ZXN0IihjaG9pY2UgQ2hvaWNlPEZISVIuZGF0ZVRpbWUsIEZISVIuUGVyaW9kLCBGSElSLlRpbWluZywgRkhJUi5pbnN0YW50LCBGSElSLnN0cmluZywgRkhJUi5BZ2UsIEZISVIuUmFuZ2U+ICk6DQogICgiTm9ybWFsaXplIEludGVydmFsIihjaG9pY2UpKSBwZXJpb2QNCiAgICByZXR1cm4NCiAgICAgIGlmICggSGFzRW5kKHBlcmlvZCkpIHRoZW4gZW5kIG9mIHBlcmlvZA0KICAgICAgZWxzZSBzdGFydCBvZiBwZXJpb2QNCg0KLypHaXZlbiBhbiBpbnRlcnZhbCwgcmV0dXJuIHRoZSBzdGFydGluZyBwb2ludCBpZiB0aGUgaW50ZXJ2YWwgaGFzIGEgc3RhcnRpbmcgYm91bmRhcnkgc3BlY2lmaWVkLCBvdGhlcndpc2UsIHJldHVybiB0aGUgZW5kaW5nIHBvaW50Ki8NCmRlZmluZSBmdW5jdGlvbiAiRWFybGllc3QiKGNob2ljZSBDaG9pY2U8RkhJUi5kYXRlVGltZSwgRkhJUi5QZXJpb2QsIEZISVIuVGltaW5nLCBGSElSLmluc3RhbnQsIEZISVIuc3RyaW5nLCBGSElSLkFnZSwgRkhJUi5SYW5nZT4gKToNCiAgKCJOb3JtYWxpemUgSW50ZXJ2YWwiKGNob2ljZSkpIHBlcmlvZA0KICAgIHJldHVybg0KICAgICAgaWYgKEhhc1N0YXJ0KHBlcmlvZCkpIHRoZW4gc3RhcnQgb2YgcGVyaW9kDQogICAgICBlbHNlIGVuZCBvZiBwZXJpb2QNCg0K"
			}
		  ]
		},
		"request": {
		  "method": "PUT",
		  "url": "Library/MATGlobalCommonFunctionsFHIR4"
		}
	  },
	  {
		"resource": {
		  "resourceType": "ValueSet",
		  "id": "2.16.840.1.114222.4.11.836",
		  "meta": {
			"versionId": "1",
			"lastUpdated": "2012-10-25T12:28:31.000-04:00"
		  },
		  "text": {
			"status": "generated",
			"div": "<div xmlns=\"http://www.w3.org/1999/xhtml\"><h3>Value Set Contents</h3><p>This value set contains 6 concepts</p><p>All codes from system <code>urn:oid:2.16.840.1.113883.6.238</code></p><table class=\"codes\"><tr><td style=\"white-space:nowrap\"><b>Code</b></td><td><b>Display</b></td></tr><tr><td style=\"white-space:nowrap\"><a name=\"urn-oid-2.16.840.1.113883.6.238-1002-5\"> </a>1002-5</td><td>American Indian or Alaska Native</td></tr><tr><td style=\"white-space:nowrap\"><a name=\"urn-oid-2.16.840.1.113883.6.238-2028-9\"> </a>2028-9</td><td>Asian</td></tr><tr><td style=\"white-space:nowrap\"><a name=\"urn-oid-2.16.840.1.113883.6.238-2054-5\"> </a>2054-5</td><td>Black or African American</td></tr><tr><td style=\"white-space:nowrap\"><a name=\"urn-oid-2.16.840.1.113883.6.238-2076-8\"> </a>2076-8</td><td>Native Hawaiian or Other Pacific Islander</td></tr><tr><td style=\"white-space:nowrap\"><a name=\"urn-oid-2.16.840.1.113883.6.238-2106-3\"> </a>2106-3</td><td>White</td></tr><tr><td style=\"white-space:nowrap\"><a name=\"urn-oid-2.16.840.1.113883.6.238-2131-1\"> </a>2131-1</td><td>Other Race</td></tr></table></div>"
		  },
		  "url": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.114222.4.11.836",
		  "version": "20121025",
		  "name": "Race",
		  "title": "Race",
		  "status": "active",
		  "date": "2012-10-25T12:28:31-04:00",
		  "publisher": "CDC NCHS",
		  "description": "The purpose of this value set is to represent CDC concepts for Race.",
		  "expansion": {
			"identifier": "urn:uuid:55002dc9-9101-423c-9850-a8b376412366",
			"timestamp": "2022-03-15T14:18:47-04:00",
			"total": 6,
			"offset": 0,
			"parameter": [
			  {
				"name": "count",
				"valueInteger": 1000
			  },
			  {
				"name": "offset",
				"valueInteger": 0
			  }
			],
			"contains": [
			  {
				"system": "urn:oid:2.16.840.1.113883.6.238",
				"version": "1.2",
				"code": "1002-5",
				"display": "American Indian or Alaska Native"
			  },
			  {
				"system": "urn:oid:2.16.840.1.113883.6.238",
				"version": "1.2",
				"code": "2028-9",
				"display": "Asian"
			  },
			  {
				"system": "urn:oid:2.16.840.1.113883.6.238",
				"version": "1.2",
				"code": "2054-5",
				"display": "Black or African American"
			  },
			  {
				"system": "urn:oid:2.16.840.1.113883.6.238",
				"version": "1.2",
				"code": "2076-8",
				"display": "Native Hawaiian or Other Pacific Islander"
			  },
			  {
				"system": "urn:oid:2.16.840.1.113883.6.238",
				"version": "1.2",
				"code": "2106-3",
				"display": "White"
			  },
			  {
				"system": "urn:oid:2.16.840.1.113883.6.238",
				"version": "1.2",
				"code": "2131-1",
				"display": "Other Race"
			  }
			]
		  }
		},
		"request": {
		  "method": "PUT",
		  "url": "ValueSet/2.16.840.1.114222.4.11.836/_history/1"
		}
	  },
	  {
		"resource": {
		  "resourceType": "ValueSet",
		  "id": "2.16.840.1.113762.1.4.1111.143",
		  "meta": {
			"versionId": "26",
			"lastUpdated": "2021-06-11T01:02:23.000-04:00"
		  },
		  "text": {
			"status": "generated",
			"div": "<div xmlns=\"http://www.w3.org/1999/xhtml\">\n         <h3>Value Set Contents</h3>\n         <p>This value set contains 1 concepts</p>\n         <p>All codes from system \n            <code>http://snomed.info/sct</code>\n         </p>\n         <table class=\"codes\">\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <b>Code</b>\n               </td>\n               <td>\n                  <b>Display</b>\n               </td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"http---snomed.info-sct-448951000124107\"> </a>\n                  <a href=\"http://browser.ihtsdotools.org/?perspective=full&amp;conceptId1=448951000124107\">448951000124107</a>\n               </td>\n               <td>Admission to observation unit (procedure)</td>\n            </tr>\n         </table>\n      </div>"
		  },
		  "url": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1111.143",
		  "version": "20210611",
		  "name": "Observation Services",
		  "title": "Observation Services",
		  "status": "active",
		  "date": "2021-06-11T01:02:23-04:00",
		  "publisher": "TJC EH Steward",
		  "description": "The purpose of this value set is to represent concepts for encounters for observation in the inpatient setting.",
		  "expansion": {
			"identifier": "urn:uuid:779caa62-0502-433f-86b6-7e36281233b0",
			"timestamp": "2022-03-09T09:26:12-05:00",
			"total": 1,
			"offset": 0,
			"parameter": [
			  {
				"name": "count",
				"valueInteger": 1000
			  },
			  {
				"name": "offset",
				"valueInteger": 0
			  }
			],
			"contains": [
			  {
				"system": "http://snomed.info/sct",
				"version": "http://snomed.info/sct/731000124108/version/20210901",
				"code": "448951000124107",
				"display": "Admission to observation unit (procedure)"
			  }
			]
		  }
		},
		"request": {
		  "method": "PUT",
		  "url": "ValueSet/2.16.840.1.113762.1.4.1111.143/_history/26"
		}
	  },
	  {
		"resource": {
		  "resourceType": "Library",
		  "id": "SupplementalDataElementsFHIR4",
		  "contained": [
			{
			  "resourceType": "Parameters",
			  "id": "options",
			  "parameter": [
				{
				  "name": "translatorVersion",
				  "valueString": "3.5.1"
				},
				{
				  "name": "option",
				  "valueString": "EnableDateRangeOptimization"
				},
				{
				  "name": "option",
				  "valueString": "EnableAnnotations"
				},
				{
				  "name": "option",
				  "valueString": "EnableLocators"
				},
				{
				  "name": "option",
				  "valueString": "DisableListDemotion"
				},
				{
				  "name": "option",
				  "valueString": "DisableListPromotion"
				},
				{
				  "name": "analyzeDataRequirements",
				  "valueBoolean": false
				},
				{
				  "name": "collapseDataRequirements",
				  "valueBoolean": true
				},
				{
				  "name": "compatibilityLevel",
				  "valueString": "1.5"
				},
				{
				  "name": "enableCqlOnly",
				  "valueBoolean": false
				},
				{
				  "name": "errorLevel",
				  "valueString": "Info"
				},
				{
				  "name": "signatureLevel",
				  "valueString": "None"
				},
				{
				  "name": "validateUnits",
				  "valueBoolean": true
				},
				{
				  "name": "verifyOnly",
				  "valueBoolean": false
				}
			  ]
			}
		  ],
		  "extension": [
			{
			  "url": "http://hl7.org/fhir/StructureDefinition/cqf-cqlOptions",
			  "valueReference": {
				"reference": "#options"
			  }
			}
		  ],
		  "url": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/SupplementalDataElementsFHIR4",
		  "version": "2.0.000",
		  "name": "SupplementalDataElementsFHIR4",
		  "status": "draft",
		  "type": {
			"coding": [
			  {
				"system": "http://terminology.hl7.org/CodeSystem/library-type",
				"code": "logic-library"
			  }
			]
		  },
		  "relatedArtifact": [
			{
			  "type": "depends-on",
			  "display": "FHIR model information",
			  "resource": "http://fhir.org/guides/cqf/common/Library/FHIR-ModelInfo|4.0.1"
			},
			{
			  "type": "depends-on",
			  "display": "Library FHIRHelpers",
			  "resource": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/FHIRHelpers|4.0.1"
			},
			{
			  "type": "depends-on",
			  "display": "Value set Ethnicity",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.114222.4.11.837"
			},
			{
			  "type": "depends-on",
			  "display": "Value set ONC Administrative Sex",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1"
			},
			{
			  "type": "depends-on",
			  "display": "Value set Payer",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.114222.4.11.3591"
			},
			{
			  "type": "depends-on",
			  "display": "Value set Race",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.114222.4.11.836"
			}
		  ],
		  "parameter": [
			{
			  "name": "Patient",
			  "use": "out",
			  "min": 0,
			  "max": "1",
			  "type": "Patient"
			},
			{
			  "name": "SDE Ethnicity",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Coding"
			},
			{
			  "name": "SDE Payer",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Coverage"
			},
			{
			  "name": "SDE Race",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Coding"
			},
			{
			  "name": "SDE Sex",
			  "use": "out",
			  "min": 0,
			  "max": "1",
			  "type": "Coding"
			}
		  ],
		  "dataRequirement": [
			{
			  "type": "Patient",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Patient"
			  ],
			  "mustSupport": [
				"url",
				"extension",
				"value"
			  ]
			},
			{
			  "type": "Coverage",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Coverage"
			  ],
			  "mustSupport": [
				"type"
			  ],
			  "codeFilter": [
				{
				  "path": "type",
				  "valueSet": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.114222.4.11.3591"
				}
			  ]
			}
		  ],
		  "content": [
			{
			  "contentType": "text/cql",
			  "data": "bGlicmFyeSBTdXBwbGVtZW50YWxEYXRhRWxlbWVudHNGSElSNCB2ZXJzaW9uICcyLjAuMDAwJw0KDQovKkB1cGRhdGU6IEBAQlRSIDIwMjAtMDMtMzEgLT4NCkluY3JlbWVudGVkIHZlcnNpb24gdG8gMi4wLjANClVwZGF0ZWQgRkhJUiB2ZXJzaW9uIHRvIDQuMC4xDQpAQEAqLw0KDQp1c2luZyBGSElSIHZlcnNpb24gJzQuMC4xJw0KDQppbmNsdWRlIEZISVJIZWxwZXJzIHZlcnNpb24gJzQuMC4xJyBjYWxsZWQgRkhJUkhlbHBlcnMNCg0KdmFsdWVzZXQgIkV0aG5pY2l0eSI6ICdodHRwOi8vY3RzLm5sbS5uaWguZ292L2ZoaXIvVmFsdWVTZXQvMi4xNi44NDAuMS4xMTQyMjIuNC4xMS44MzcnIA0KdmFsdWVzZXQgIk9OQyBBZG1pbmlzdHJhdGl2ZSBTZXgiOiAnaHR0cDovL2N0cy5ubG0ubmloLmdvdi9maGlyL1ZhbHVlU2V0LzIuMTYuODQwLjEuMTEzNzYyLjEuNC4xJyANCnZhbHVlc2V0ICJQYXllciI6ICdodHRwOi8vY3RzLm5sbS5uaWguZ292L2ZoaXIvVmFsdWVTZXQvMi4xNi44NDAuMS4xMTQyMjIuNC4xMS4zNTkxJyANCnZhbHVlc2V0ICJSYWNlIjogJ2h0dHA6Ly9jdHMubmxtLm5paC5nb3YvZmhpci9WYWx1ZVNldC8yLjE2Ljg0MC4xLjExNDIyMi40LjExLjgzNicgDQoNCmNvbnRleHQgUGF0aWVudA0KDQpkZWZpbmUgIlNERSBFdGhuaWNpdHkiOg0KICAoZmxhdHRlbiAoDQogICAgICBQYXRpZW50LmV4dGVuc2lvbiBFeHRlbnNpb24NCiAgICAgICAgd2hlcmUgRXh0ZW5zaW9uLnVybCA9ICdodHRwOi8vaGw3Lm9yZy9maGlyL3VzL2NvcmUvU3RydWN0dXJlRGVmaW5pdGlvbi91cy1jb3JlLWV0aG5pY2l0eScNCiAgICAgICAgICByZXR1cm4gRXh0ZW5zaW9uLmV4dGVuc2lvbg0KICAgICkpIEUNCiAgICAgIHdoZXJlIEUudXJsID0gJ29tYkNhdGVnb3J5Jw0KICAgICAgICBvciBFLnVybCA9ICdkZXRhaWxlZCcNCiAgICAgIHJldHVybiBFLnZhbHVlIGFzIENvZGluZw0KDQpkZWZpbmUgIlNERSBQYXllciI6DQogIFtDb3ZlcmFnZTogdHlwZSBpbiAiUGF5ZXIiXSBQYXllcg0KDQpkZWZpbmUgIlNERSBSYWNlIjoNCiAgKGZsYXR0ZW4gKA0KICAgICAgUGF0aWVudC5leHRlbnNpb24gRXh0ZW5zaW9uDQogICAgICAgIHdoZXJlIEV4dGVuc2lvbi51cmwgPSAnaHR0cDovL2hsNy5vcmcvZmhpci91cy9jb3JlL1N0cnVjdHVyZURlZmluaXRpb24vdXMtY29yZS1yYWNlJw0KICAgICAgICAgIHJldHVybiBFeHRlbnNpb24uZXh0ZW5zaW9uDQogICAgKSkgRQ0KICAgICAgd2hlcmUgRS51cmwgPSAnb21iQ2F0ZWdvcnknDQogICAgICAgIG9yIEUudXJsID0gJ2RldGFpbGVkJw0KICAgICAgcmV0dXJuIEUudmFsdWUgYXMgQ29kaW5nDQoNCmRlZmluZSAiU0RFIFNleCI6DQogIGNhc2UNCiAgICAgIHdoZW4gUGF0aWVudC5nZW5kZXIgPSAnbWFsZScgdGhlbiBDb2RlIHsgY29kZTogJ00nLCBzeXN0ZW06ICdodHRwOi8vaGw3Lm9yZy9maGlyL3YzL0FkbWluaXN0cmF0aXZlR2VuZGVyJywgZGlzcGxheTogJ01hbGUnIH0NCiAgICAgIHdoZW4gUGF0aWVudC5nZW5kZXIgPSAnZmVtYWxlJyB0aGVuIENvZGUgeyBjb2RlOiAnRicsIHN5c3RlbTogJ2h0dHA6Ly9obDcub3JnL2ZoaXIvdjMvQWRtaW5pc3RyYXRpdmVHZW5kZXInLCBkaXNwbGF5OiAnRmVtYWxlJyB9DQogICAgICBlbHNlIG51bGwNCiAgICBlbmQNCg0K"
			}
		  ]
		},
		"request": {
		  "method": "PUT",
		  "url": "Library/SupplementalDataElementsFHIR4"
		}
	  },
	  {
		"resource": {
		  "resourceType": "ValueSet",
		  "id": "2.16.840.1.113883.3.666.5.307",
		  "meta": {
			"versionId": "33",
			"lastUpdated": "2021-01-19T09:34:06.000-05:00"
		  },
		  "text": {
			"status": "generated",
			"div": "<div xmlns=\"http://www.w3.org/1999/xhtml\">\n         <h3>Value Set Contents</h3>\n         <p>This value set contains 3 concepts</p>\n         <p>All codes from system \n            <code>http://snomed.info/sct</code>\n         </p>\n         <table class=\"codes\">\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <b>Code</b>\n               </td>\n               <td>\n                  <b>Display</b>\n               </td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"http---snomed.info-sct-183452005\"> </a>\n                  <a href=\"http://browser.ihtsdotools.org/?perspective=full&amp;conceptId1=183452005\">183452005</a>\n               </td>\n               <td>Emergency hospital admission (procedure)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"http---snomed.info-sct-32485007\"> </a>\n                  <a href=\"http://browser.ihtsdotools.org/?perspective=full&amp;conceptId1=32485007\">32485007</a>\n               </td>\n               <td>Hospital admission (procedure)</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"http---snomed.info-sct-8715000\"> </a>\n                  <a href=\"http://browser.ihtsdotools.org/?perspective=full&amp;conceptId1=8715000\">8715000</a>\n               </td>\n               <td>Hospital admission, elective (procedure)</td>\n            </tr>\n         </table>\n      </div>"
		  },
		  "url": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307",
		  "version": "20200307",
		  "name": "Encounter Inpatient",
		  "title": "Encounter Inpatient",
		  "status": "active",
		  "date": "2020-03-07T01:00:21-05:00",
		  "publisher": "Lantana EH Steward",
		  "description": "The purpose of this value set is to represent concepts of inpatient hospitalization encounters.",
		  "expansion": {
			"identifier": "urn:uuid:b1561d23-7da0-4382-b765-08d32dbf48c2",
			"timestamp": "2022-03-09T10:11:35-05:00",
			"total": 3,
			"offset": 0,
			"parameter": [
			  {
				"name": "count",
				"valueInteger": 1000
			  },
			  {
				"name": "offset",
				"valueInteger": 0
			  }
			],
			"contains": [
			  {
				"system": "http://snomed.info/sct",
				"version": "http://snomed.info/sct/731000124108/version/20210901",
				"code": "183452005",
				"display": "Emergency hospital admission (procedure)"
			  },
			  {
				"system": "http://snomed.info/sct",
				"version": "http://snomed.info/sct/731000124108/version/20210901",
				"code": "32485007",
				"display": "Hospital admission (procedure)"
			  },
			  {
				"system": "http://snomed.info/sct",
				"version": "http://snomed.info/sct/731000124108/version/20210901",
				"code": "8715000",
				"display": "Hospital admission, elective (procedure)"
			  }
			]
		  }
		},
		"request": {
		  "method": "PUT",
		  "url": "ValueSet/2.16.840.1.113883.3.666.5.307/_history/33"
		}
	  },
	  {
		"resource": {
		  "resourceType": "ValueSet",
		  "id": "2.16.840.1.113762.1.4.1046.274",
		  "meta": {
			"versionId": "7",
			"lastUpdated": "2024-01-23T01:10:32.000-05:00",
			"profile": [
			  "http://hl7.org/fhir/StructureDefinition/shareablevalueset",
			  "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/computable-valueset-cqfm",
			  "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/publishable-valueset-cqfm"
			]
		  },
		  "extension": [
			{
			  "url": "http://hl7.org/fhir/StructureDefinition/valueset-author",
			  "valueString": "Lantana Author"
			},
			{
			  "url": "http://hl7.org/fhir/StructureDefinition/resource-lastReviewDate",
			  "valueDate": "2024-01-23"
			},
			{
			  "url": "http://hl7.org/fhir/StructureDefinition/valueset-effectiveDate",
			  "valueDate": "2024-01-23"
			}
		  ],
		  "url": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1046.274",
		  "identifier": [
			{
			  "system": "urn:ietf:rfc:3986",
			  "value": "urn:oid:2.16.840.1.113762.1.4.1046.274"
			}
		  ],
		  "version": "20240123",
		  "name": "NHSNInpatientEncounterClassCodes",
		  "title": "NHSN Inpatient Encounter Class Codes",
		  "status": "active",
		  "date": "2024-01-23T01:10:32-05:00",
		  "publisher": "Lantana",
		  "jurisdiction": [
			{
			  "extension": [
				{
				  "url": "http://hl7.org/fhir/StructureDefinition/data-absent-reason",
				  "valueCode": "unknown"
				}
			  ]
			}
		  ],
		  "purpose": "(Clinical Focus: The purpose of this value set is to represent concepts of inpatient hospital encounters.),(Data Element Scope: This value set includes class codes used to indicate an inpatient encounter.),(Inclusion Criteria: Includes concepts that represent an encounter for inpatient hospitalizations, including emergency department visits and observation visits associated with the inpatient hospitalization.),(Exclusion Criteria: Any class that does not represent inpatient hospitalizations such as ambulatory and virtual visits.)",
		  "compose": {
			"include": [
			  {
				"system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
				"concept": [
				  {
					"code": "ACUTE",
					"display": "inpatient acute"
				  },
				  {
					"code": "IMP",
					"display": "inpatient encounter"
				  },
				  {
					"code": "NONAC",
					"display": "inpatient non-acute"
				  },
				  {
					"code": "SS",
					"display": "short stay"
				  }
				]
			  }
			]
		  }
		},
		"request": {
		  "method": "PUT",
		  "url": "ValueSet/2.16.840.1.113762.1.4.1046.274/_history/7"
		}
	  },
	  {
		"resource": {
		  "resourceType": "ValueSet",
		  "id": "2.16.840.1.113762.1.4.1046.265",
		  "meta": {
			"versionId": "16",
			"lastUpdated": "2022-07-20T01:02:37.000-04:00"
		  },
		  "url": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1046.265",
		  "version": "20220720",
		  "name": "Inpatient, Emergency, and Observation Locations",
		  "status": "active",
		  "date": "2022-07-20T01:02:37-04:00",
		  "publisher": "CDC NHSN",
		  "expansion": {
			"identifier": "urn:uuid:56f6d6af-436a-4954-b60f-cf4deb1a8947",
			"timestamp": "2022-08-13T14:22:35-04:00",
			"total": 111,
			"offset": 0,
			"parameter": [
			  {
				"name": "count",
				"valueInteger": 1000
			  },
			  {
				"name": "offset",
				"valueInteger": 0
			  }
			],
			"contains": [
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1005-8",
				"display": "Cardiac Catheterization Room/Suite"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1020-7",
				"display": "Sleep Study Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1025-6",
				"display": "Trauma Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1026-4",
				"display": "Burn Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1027-2",
				"display": "Medical Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1028-0",
				"display": "Medical Cardiac Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1029-8",
				"display": "Medical-Surgical Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1030-6",
				"display": "Surgical Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1031-4",
				"display": "Neurosurgical Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1032-2",
				"display": "Surgical Cardiothoracic Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1033-0",
				"display": "Respiratory Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1034-8",
				"display": "Prenatal Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1035-5",
				"display": "Neurologic Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1038-9",
				"display": "Well Newborn Nursery (Level I)"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1039-7",
				"display": "Neonatal Critical Care (Level II/III)"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1040-5",
				"display": "Neonatal Critical Care (Level III)"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1041-3",
				"display": "Special Care Nursery (Level II)"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1042-1",
				"display": "Pediatric Burn Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1043-9",
				"display": "Pediatric Surgical Cardiothoracic Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1044-7",
				"display": "Pediatric Medical Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1045-4",
				"display": "Pediatric Medical-Surgical Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1046-2",
				"display": "Pediatric Neurosurgical Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1047-0",
				"display": "Pediatric Respiratory Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1048-8",
				"display": "Pediatric Surgical Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1049-6",
				"display": "Pediatric Trauma Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1051-2",
				"display": "Behavioral Health/Psych Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1052-0",
				"display": "Burn Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1053-8",
				"display": "Ear, Nose, Throat Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1054-6",
				"display": "Gastrointestinal Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1055-3",
				"display": "Genitourinary Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1056-1",
				"display": "Gerontology Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1057-9",
				"display": "Gynecology Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1058-7",
				"display": "Labor and Delivery Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1059-5",
				"display": "Labor, Delivery, Recovery, Postpartum Suite"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1060-3",
				"display": "Medical Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1061-1",
				"display": "Medical-Surgical Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1062-9",
				"display": "Neurology Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1063-7",
				"display": "Neurosurgical Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1064-5",
				"display": "Ophthalmology Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1065-2",
				"display": "Orthopedic Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1066-0",
				"display": "Orthopedic Trauma Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1067-8",
				"display": "Plastic Surgery Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1068-6",
				"display": "Postpartum Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1069-4",
				"display": "Pulmonary Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1070-2",
				"display": "Rehabilitation Ward (within Hospital)"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1071-0",
				"display": "Stroke (Acute) Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1072-8",
				"display": "Surgical Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1073-6",
				"display": "Vascular Surgery Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1075-1",
				"display": "Adolescent Behavioral Health Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1076-9",
				"display": "Pediatric Medical Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1077-7",
				"display": "Pediatric Behavioral Health Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1078-5",
				"display": "Pediatric Burn Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1079-3",
				"display": "Pediatric Ear, Nose, Throat Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1080-1",
				"display": "Pediatric Genitourinary Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1081-9",
				"display": "Pediatric Medical-Surgical Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1082-7",
				"display": "Pediatric Neurology Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1083-5",
				"display": "Pediatric Neurosurgical Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1084-3",
				"display": "Pediatric Orthopedic Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1085-0",
				"display": "Pediatric Rehabilitation Ward (within Hospital)"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1086-8",
				"display": "Pediatric Surgical Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1091-8",
				"display": "Pediatric Dialysis Specialty Care Area"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1092-6",
				"display": "Solid Organ Transplant Specialty Care Area"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1093-4",
				"display": "Pediatric Solid Organ Transplant Specialty Care Area"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1095-9",
				"display": "Cesarean Section Room/Suite"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1096-7",
				"display": "Operating Room/Suite"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1097-5",
				"display": "Post-Anesthesia Care Unit/Recovery Room"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1099-1",
				"display": "Adult Step Down Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1100-7",
				"display": "Pediatric Step Down Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1102-3",
				"display": "Chronic Care Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1103-1",
				"display": "Chronic Alzheimer's Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1104-9",
				"display": "Chronic Behavioral Health/Psych Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1105-6",
				"display": "Chronic Rehabilitation Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1108-0",
				"display": "Emergency Department"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1109-8",
				"display": "Pediatric Emergency Department"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1162-7",
				"display": "24-Hour Observation Area"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1164-3",
				"display": "Ventilator Dependent Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1165-0",
				"display": "Inpatient Hospice"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1171-8",
				"display": "Jail Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1172-6",
				"display": "School Infirmary"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1198-1",
				"display": "Dialysis Specialty Care Area"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1203-9",
				"display": "Interventional Radiology"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1205-4",
				"display": "Antenatal Care Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1208-8",
				"display": "Telemetry Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1209-6",
				"display": "Treatment Room"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1210-4",
				"display": "Adult Mixed Acuity Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1211-2",
				"display": "Pediatric Mixed Acuity Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1212-0",
				"display": "Mixed Age Mixed Acuity Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1214-6",
				"display": "Long Term Acute Care Pediatric Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1217-9",
				"display": "Rehabilitation Ward (within freestanding Inpatient Rehabilitation Facility)"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1218-7",
				"display": "Pediatric Rehabilitation Ward (within freestanding Inpatient Rehabilitation Facility)"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1220-3",
				"display": "Long Term Acute Care Intensive Care Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1221-1",
				"display": "Long Term Acute Care Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1222-9",
				"display": "Long Term Acute Care Pediatric Intensive Care Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1223-7",
				"display": "Oncology Medical Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1224-5",
				"display": "Oncology Surgical Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1225-2",
				"display": "Oncology Medical-Surgical Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1226-0",
				"display": "Oncology Leukemia Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1227-8",
				"display": "Oncology Step Down Unit"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1228-6",
				"display": "Oncology Lymphoma Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1229-4",
				"display": "Oncology Leukemia-Lymphoma Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1230-2",
				"display": "Oncology Solid Tumor Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1231-0",
				"display": "Oncology Hematopoietic Stem Cell Transplant Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1232-8",
				"display": "Oncology General Hematology-Oncology Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1233-6",
				"display": "Oncology Pediatric Critical Care"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1234-4",
				"display": "Oncology Pediatric Hematopoietic Stem Cell Transplant Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1235-1",
				"display": "Oncology Pediatric General Hematology-Oncology Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1236-9",
				"display": "Oncology Mixed Acuity Unit (all ages)"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1269-0",
				"display": "Neonatal Critical Care (Level IV)"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1270-8",
				"display": "Chemical Dependency Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1271-6",
				"display": "Onsite Overflow Ward"
			  },
			  {
				"system": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
				"version": "2022",
				"code": "1272-4",
				"display": "Onsite Overflow Critical Care"
			  }
			]
		  }
		},
		"request": {
		  "method": "PUT",
		  "url": "ValueSet/2.16.840.1.113762.1.4.1046.265/_history/16"
		}
	  },
	  {
		"resource": {
		  "resourceType": "Library",
		  "id": "SharedResourceCreation",
		  "contained": [
			{
			  "resourceType": "Parameters",
			  "id": "options",
			  "parameter": [
				{
				  "name": "translatorVersion",
				  "valueString": "3.5.1"
				},
				{
				  "name": "option",
				  "valueString": "EnableDateRangeOptimization"
				},
				{
				  "name": "option",
				  "valueString": "EnableAnnotations"
				},
				{
				  "name": "option",
				  "valueString": "EnableLocators"
				},
				{
				  "name": "option",
				  "valueString": "DisableListDemotion"
				},
				{
				  "name": "option",
				  "valueString": "DisableListPromotion"
				},
				{
				  "name": "analyzeDataRequirements",
				  "valueBoolean": false
				},
				{
				  "name": "collapseDataRequirements",
				  "valueBoolean": true
				},
				{
				  "name": "compatibilityLevel",
				  "valueString": "1.5"
				},
				{
				  "name": "enableCqlOnly",
				  "valueBoolean": false
				},
				{
				  "name": "errorLevel",
				  "valueString": "Info"
				},
				{
				  "name": "signatureLevel",
				  "valueString": "None"
				},
				{
				  "name": "validateUnits",
				  "valueBoolean": true
				},
				{
				  "name": "verifyOnly",
				  "valueBoolean": false
				}
			  ]
			}
		  ],
		  "extension": [
			{
			  "url": "http://hl7.org/fhir/StructureDefinition/cqf-cqlOptions",
			  "valueReference": {
				"reference": "#options"
			  }
			}
		  ],
		  "url": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/SharedResourceCreation",
		  "version": "0.1.005",
		  "name": "SharedResourceCreation",
		  "status": "draft",
		  "type": {
			"coding": [
			  {
				"system": "http://terminology.hl7.org/CodeSystem/library-type",
				"code": "logic-library"
			  }
			]
		  },
		  "relatedArtifact": [
			{
			  "type": "depends-on",
			  "display": "FHIR model information",
			  "resource": "http://fhir.org/guides/cqf/common/Library/FHIR-ModelInfo|4.0.1"
			},
			{
			  "type": "depends-on",
			  "display": "Library FHIRHelpers",
			  "resource": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/FHIRHelpers|4.0.1"
			}
		  ],
		  "content": [
			{
			  "contentType": "text/cql",
			  "data": "bGlicmFyeSBTaGFyZWRSZXNvdXJjZUNyZWF0aW9uIHZlcnNpb24gJzAuMS4wMDUnDQoNCnVzaW5nIEZISVIgdmVyc2lvbiAnNC4wLjEnDQoNCmluY2x1ZGUgRkhJUkhlbHBlcnMgdmVyc2lvbiAnNC4wLjEnIGNhbGxlZCBGSElSSGVscGVycw0KDQpkZWZpbmUgZnVuY3Rpb24gIkdldElkRXh0ZW5zaW9ucyIoZG9tYWluUmVzb3VyY2UgRG9tYWluUmVzb3VyY2UpOg0KICBkb21haW5SZXNvdXJjZS5leHRlbnNpb24gRQ0KICB3aGVyZSBFLnVybCA9ICdodHRwOi8vd3d3LmNkYy5nb3Yvbmhzbi9maGlycG9ydGFsL2RxbS9pZy9TdHJ1Y3R1cmVEZWZpbml0aW9uL2xpbmstb3JpZ2luYWwtcmVzb3VyY2UtaWQtZXh0ZW5zaW9uJw0KICByZXR1cm4gRQ0KDQpkZWZpbmUgZnVuY3Rpb24gIkdldFBhdGllbnRFeHRlbnNpb25zIihkb21haW5SZXNvdXJjZSBEb21haW5SZXNvdXJjZSk6DQogIGRvbWFpblJlc291cmNlLmV4dGVuc2lvbiBFDQogIHdoZXJlIEUudXJsID0gJ2h0dHA6Ly9obDcub3JnL2ZoaXIvdXMvY29yZS9TdHJ1Y3R1cmVEZWZpbml0aW9uL3VzLWNvcmUtcmFjZScNCiAgICBvciBFLnVybCA9ICdodHRwOi8vaGw3Lm9yZy9maGlyL3VzL2NvcmUvU3RydWN0dXJlRGVmaW5pdGlvbi91cy1jb3JlLWV0aG5pY2l0eScNCiAgICBvciBFLnVybCA9ICdodHRwOi8vaGw3Lm9yZy9maGlyL3VzL2NvcmUvU3RydWN0dXJlRGVmaW5pdGlvbi91cy1jb3JlLWJpcnRoc2V4Jw0KICAgIG9yIEUudXJsID0gJ2h0dHA6Ly9obDcub3JnL2ZoaXIvdXMvY29yZS9TdHJ1Y3R1cmVEZWZpbml0aW9uL3VzLWNvcmUtZ2VuZGVySWRlbnRpdHknDQogICAgb3IgRS51cmwgPSAnaHR0cDovL2hsNy5vcmcvZmhpci9TdHJ1Y3R1cmVEZWZpbml0aW9uL3BhdGllbnQtZ2VuZGVySWRlbnRpdHknDQogICAgb3IgRS51cmwgPSAnaHR0cDovL3d3dy5jZGMuZ292L25oc24vZmhpcnBvcnRhbC9kcW0vaWcvU3RydWN0dXJlRGVmaW5pdGlvbi9saW5rLW9yaWdpbmFsLXJlc291cmNlLWlkLWV4dGVuc2lvbicNCiAgcmV0dXJuIEUNCg0KZGVmaW5lIGZ1bmN0aW9uICJNZXRhRWxlbWVudCIocmVzb3VyY2UgUmVzb3VyY2UsIHByb2ZpbGVVUkxzIExpc3Q8RkhJUi5jYW5vbmljYWw+KToNCiAgcmVzb3VyY2Ugcg0KICByZXR1cm4gRkhJUi5NZXRhew0KICAgIGV4dGVuc2lvbjogci5tZXRhLmV4dGVuc2lvbiwNCiAgICB2ZXJzaW9uSWQ6IHIubWV0YS52ZXJzaW9uSWQsDQogICAgbGFzdFVwZGF0ZWQ6IHIubWV0YS5sYXN0VXBkYXRlZCwNCiAgICBwcm9maWxlOiBwcm9maWxlVVJMcywNCiAgICBzZWN1cml0eTogci5tZXRhLnNlY3VyaXR5LA0KICAgIHRhZzogci5tZXRhLnRhZw0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBDb25kaXRpb25TdGFnZShzdGFnZSBMaXN0PEZISVIuQ29uZGl0aW9uLlN0YWdlPik6DQogIHN0YWdlIHMNCiAgcmV0dXJuIEZISVIuQ29uZGl0aW9uLlN0YWdlew0KICAgIHN1bW1hcnk6IHMuc3VtbWFyeSwNCiAgICBhc3Nlc3NtZW50OiBzLmFzc2Vzc21lbnQsDQogICAgdHlwZTogcy50eXBlDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIENvbmRpdGlvbkV2aWRlbmNlKGV2aWRlbmNlIExpc3Q8RkhJUi5Db25kaXRpb24uRXZpZGVuY2U+KToNCiAgZXZpZGVuY2UgZQ0KICByZXR1cm4gRkhJUi5Db25kaXRpb24uRXZpZGVuY2V7DQogICAgY29kZTogZS5jb2RlLA0KICAgIGRldGFpbDogZS5kZXRhaWwNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gQ29uZGl0aW9uUmVzb3VyY2UoY29uZGl0aW9uIENvbmRpdGlvbiwgcHJvZmlsZVVSTHMgTGlzdDxGSElSLmNhbm9uaWNhbD4pOg0KICBjb25kaXRpb24gYw0KICByZXR1cm4gQ29uZGl0aW9uew0KICAgIGlkOiBGSElSLmlkIHt2YWx1ZTogJ0xDUi0nICsgYy5pZH0sDQogICAgbWV0YTogTWV0YUVsZW1lbnQoYywgcHJvZmlsZVVSTHMpLA0KICAgIGV4dGVuc2lvbjogYy5leHRlbnNpb24sDQogICAgY2xpbmljYWxTdGF0dXM6IGMuY2xpbmljYWxTdGF0dXMsDQogICAgdmVyaWZpY2F0aW9uU3RhdHVzOiBjLnZlcmlmaWNhdGlvblN0YXR1cywNCiAgICBjYXRlZ29yeTogYy5jYXRlZ29yeSwNCiAgICBzZXZlcml0eTogYy5zZXZlcml0eSwNCiAgICBjb2RlOiBjLmNvZGUsDQogICAgYm9keVNpdGU6IGMuYm9keVNpdGUsDQogICAgc3ViamVjdDogYy5zdWJqZWN0LA0KICAgIGVuY291bnRlcjogYy5lbmNvdW50ZXIsDQogICAgb25zZXQ6IGMub25zZXQsDQogICAgYWJhdGVtZW50OiBjLmFiYXRlbWVudCwNCiAgICByZWNvcmRlZERhdGU6IGMucmVjb3JkZWREYXRlLA0KICAgIHN0YWdlOiBDb25kaXRpb25TdGFnZShjLnN0YWdlKSwNCiAgICBldmlkZW5jZTogQ29uZGl0aW9uRXZpZGVuY2UoYy5ldmlkZW5jZSksDQogICAgbm90ZTogYy5ub3RlDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIENvdmVyYWdlQ2xhc3MoY2xhc3MgTGlzdDxGSElSLkNvdmVyYWdlLkNsYXNzPik6DQogIGNsYXNzIGMNCiAgcmV0dXJuIEZISVIuQ292ZXJhZ2UuQ2xhc3N7DQogICAgdmFsdWU6IGMudmFsdWUsDQogICAgbmFtZTogYy5uYW1lDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIENvdmVyYWdlUmVzb3VyY2UoY292ZXJhZ2UgQ292ZXJhZ2UsIHByb2ZpbGVVUkxzIExpc3Q8RkhJUi5jYW5vbmljYWw+KToNCiAgY292ZXJhZ2UgYw0KICByZXR1cm4gQ292ZXJhZ2V7DQogICAgaWQ6IEZISVIuaWR7dmFsdWU6ICdMQ1ItJyArIGMuaWR9LA0KICAgIG1ldGE6IE1ldGFFbGVtZW50KGMsIHByb2ZpbGVVUkxzKSwNCiAgICBleHRlbnNpb246IGMuZXh0ZW5zaW9uLA0KICAgIHN0YXR1czogYy5zdGF0dXMsDQogICAgdHlwZTogYy50eXBlLA0KICAgIHBvbGljeUhvbGRlcjogYy5wb2xpY3lIb2xkZXIsDQogICAgc3Vic2NyaWJlcjogYy5zdWJzY3JpYmVyLA0KICAgIHN1YnNjcmliZXJJZDogYy5zdWJzY3JpYmVySWQsDQogICAgYmVuZWZpY2lhcnk6IGMuYmVuZWZpY2lhcnksDQogICAgZGVwZW5kZW50OiBjLmRlcGVuZGVudCwNCiAgICByZWxhdGlvbnNoaXA6IGMucmVsYXRpb25zaGlwLA0KICAgIHBlcmlvZDogYy5wZXJpb2QsDQogICAgcGF5b3I6IGMucGF5b3IsDQogICAgY2xhc3M6IENvdmVyYWdlQ2xhc3MoYy5jbGFzcyksDQogICAgb3JkZXI6IGMub3JkZXIsDQogICAgbmV0d29yazogYy5uZXR3b3JrLA0KICAgIHN1YnJvZ2F0aW9uOiBjLnN1YnJvZ2F0aW9uLA0KICAgIGNvbnRyYWN0OiBjLmNvbnRyYWN0DQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIERpYWdub3N0aWNSZXBvcnRDb2RpbmcoY29kaW5nIExpc3Q8Q29kaW5nPik6DQogIGNvZGluZyBjDQogIHJldHVybiBDb2Rpbmd7DQogICAgc3lzdGVtOiBjLnN5c3RlbSwNCiAgICB2ZXJzaW9uOiBjLnZlcnNpb24sDQogICAgY29kZTogYy5jb2RlLA0KICAgIGRpc3BsYXk6IGMuZGlzcGxheSwNCiAgICB1c2VyU2VsZWN0ZWQ6IGMudXNlclNlbGVjdGVkDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIERpYWdub3N0aWNSZXBvcnRDYXRlZ29yeShjYXRlZ29yeSBMaXN0PENvZGVhYmxlQ29uY2VwdD4pOg0KICBjYXRlZ29yeSBjDQogIHJldHVybiBDb2RlYWJsZUNvbmNlcHR7DQogICAgY29kaW5nOiBEaWFnbm9zdGljUmVwb3J0Q29kaW5nKGMuY29kaW5nKQ0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBEaWFnbm9zdGljUmVwb3J0TGFiUmVzb3VyY2UoZGlhZ25vc3RpY1JlcG9ydCBEaWFnbm9zdGljUmVwb3J0LCBwcm9maWxlVVJMcyBMaXN0PEZISVIuY2Fub25pY2FsPik6DQogIGRpYWdub3N0aWNSZXBvcnQgZA0KICByZXR1cm4gRGlhZ25vc3RpY1JlcG9ydHsNCiAgICBpZDogRkhJUi5pZHt2YWx1ZTogJ0xDUi0nICsgZC5pZH0sDQogICAgbWV0YTogTWV0YUVsZW1lbnQoZCwgcHJvZmlsZVVSTHMpLA0KICAgIGV4dGVuc2lvbjogZC5leHRlbnNpb24sDQogICAgYmFzZWRPbjogZC5iYXNlZE9uLA0KICAgIHN0YXR1czogZC5zdGF0dXMsDQogICAgY2F0ZWdvcnk6IERpYWdub3N0aWNSZXBvcnRDYXRlZ29yeShkLmNhdGVnb3J5KSwNCiAgICBjb2RlOiBkLmNvZGUsDQogICAgc3ViamVjdDogZC5zdWJqZWN0LA0KICAgIGVuY291bnRlcjogZC5lbmNvdW50ZXIsDQogICAgZWZmZWN0aXZlOiBkLmVmZmVjdGl2ZSwNCiAgICBpc3N1ZWQ6IGQuaXNzdWVkLA0KICAgIHBlcmZvcm1lcjogZC5wZXJmb3JtZXIsDQogICAgcmVzdWx0c0ludGVycHJldGVyOiBkLnJlc3VsdHNJbnRlcnByZXRlciwNCiAgICBzcGVjaW1lbjogZC5zcGVjaW1lbiwNCiAgICByZXN1bHQ6IGQucmVzdWx0LA0KICAgIGNvbmNsdXNpb246IGQuY29uY2x1c2lvbiwNCiAgICBjb25jbHVzaW9uQ29kZTogZC5jb25jbHVzaW9uQ29kZQ0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBFbmNvdW50ZXJJZGVudGlmaWVyKGlkZW50aWZpZXIgTGlzdDxGSElSLklkZW50aWZpZXI+KToNCiAgaWRlbnRpZmllciBpDQogIHJldHVybiBGSElSLklkZW50aWZpZXJ7DQogICAgdXNlOiBpLnVzZSwNCiAgICB0eXBlOiBpLnR5cGUsDQogICAgc3lzdGVtOiBpLnN5c3RlbSwNCiAgICB2YWx1ZTogaS52YWx1ZSwNCiAgICBwZXJpb2Q6IGkucGVyaW9kDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIEVuY291bnRlclN0YXR1c0hpc3Rvcnkoc3RhdHVzSGlzdG9yeSBMaXN0PEZISVIuRW5jb3VudGVyLlN0YXR1c0hpc3Rvcnk+KToNCiAgc3RhdHVzSGlzdG9yeSBzSA0KICByZXR1cm4gRkhJUi5FbmNvdW50ZXIuU3RhdHVzSGlzdG9yeXsNCiAgICBzdGF0dXM6IHNILnN0YXR1cywNCiAgICBwZXJpb2Q6IHNILnBlcmlvZA0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBFbmNvdW50ZXJDbGFzc0hpc3RvcnkoY2xhc3NIaXN0b3J5IExpc3Q8RkhJUi5FbmNvdW50ZXIuQ2xhc3NIaXN0b3J5Pik6DQogIGNsYXNzSGlzdG9yeSBjSA0KICByZXR1cm4gRkhJUi5FbmNvdW50ZXIuQ2xhc3NIaXN0b3J5ew0KICAgIGNsYXNzOiBjSC5jbGFzcywNCiAgICBwZXJpb2Q6IGNILnBlcmlvZA0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBFbmNvdW50ZXJQYXJ0aWNpcGFudChwYXJ0aWNpcGFudCBMaXN0PEZISVIuRW5jb3VudGVyLlBhcnRpY2lwYW50Pik6DQogIHBhcnRpY2lwYW50IHANCiAgcmV0dXJuIEZISVIuRW5jb3VudGVyLlBhcnRpY2lwYW50ew0KICAgIHR5cGU6IHAudHlwZSwNCiAgICBwZXJpb2Q6IHAucGVyaW9kLA0KICAgIGluZGl2aWR1YWw6IHAuaW5kaXZpZHVhbA0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBFbmNvdW50ZXJEaWFnbm9zaXMoZGlhZ25vc2lzIExpc3Q8RkhJUi5FbmNvdW50ZXIuRGlhZ25vc2lzPik6DQogIGRpYWdub3NpcyBkDQogIHJldHVybiBGSElSLkVuY291bnRlci5EaWFnbm9zaXN7DQogICAgY29uZGl0aW9uOiBkLmNvbmRpdGlvbiwNCiAgICB1c2U6IGQudXNlLA0KICAgIHJhbms6IGQucmFuaw0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBFbmNvdW50ZXJIb3NwaXRhbGl6YXRpb24oaG9zcGl0YWxpemF0aW9uIEZISVIuRW5jb3VudGVyLkhvc3BpdGFsaXphdGlvbik6DQogIGhvc3BpdGFsaXphdGlvbiBoDQogIHJldHVybiBGSElSLkVuY291bnRlci5Ib3NwaXRhbGl6YXRpb257DQogICAgcHJlQWRtaXNzaW9uSWRlbnRpZmllcjogaC5wcmVBZG1pc3Npb25JZGVudGlmaWVyLA0KICAgIG9yaWdpbjogaC5vcmlnaW4sDQogICAgYWRtaXRTb3VyY2U6IGguYWRtaXRTb3VyY2UsDQogICAgcmVBZG1pc3Npb246IGgucmVBZG1pc3Npb24sDQogICAgZGlldFByZWZlcmVuY2U6IGguZGlldFByZWZlcmVuY2UsDQogICAgc3BlY2lhbENvdXJ0ZXN5OiBoLnNwZWNpYWxDb3VydGVzeSwNCiAgICBzcGVjaWFsQXJyYW5nZW1lbnQ6IGguc3BlY2lhbEFycmFuZ2VtZW50LA0KICAgIGRlc3RpbmF0aW9uOiBoLmRlc3RpbmF0aW9uLA0KICAgIGRpc2NoYXJnZURpc3Bvc2l0aW9uOiBoLmRpc2NoYXJnZURpc3Bvc2l0aW9uDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIEVuY291bnRlckxvY2F0aW9uKGxvY2F0aW9uIExpc3Q8RkhJUi5FbmNvdW50ZXIuTG9jYXRpb24+KToNCiAgbG9jYXRpb24gbA0KICByZXR1cm4gRkhJUi5FbmNvdW50ZXIuTG9jYXRpb257DQogICAgbG9jYXRpb246IGwubG9jYXRpb24sDQogICAgc3RhdHVzOiBsLnN0YXR1cywNCiAgICBwaHlzaWNhbFR5cGU6IGwucGh5c2ljYWxUeXBlLA0KICAgIHBlcmlvZDogbC5wZXJpb2QNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gRW5jb3VudGVyUmVzb3VyY2UoZW5jb3VudGVyIEVuY291bnRlciwgcHJvZmlsZVVSTHMgTGlzdDxGSElSLmNhbm9uaWNhbD4pOg0KICBlbmNvdW50ZXIgZQ0KICByZXR1cm4gRW5jb3VudGVyew0KICAgIGlkOiBGSElSLmlke3ZhbHVlOiAnTENSLScgKyBlLmlkfSwNCiAgICBtZXRhOiBNZXRhRWxlbWVudChlLCBwcm9maWxlVVJMcyksDQogICAgZXh0ZW5zaW9uOiBlLmV4dGVuc2lvbiwNCiAgICBpZGVudGlmaWVyOiBFbmNvdW50ZXJJZGVudGlmaWVyKGUuaWRlbnRpZmllciksDQogICAgc3RhdHVzOiBlLnN0YXR1cywNCiAgICBzdGF0dXNIaXN0b3J5OiBFbmNvdW50ZXJTdGF0dXNIaXN0b3J5KGUuc3RhdHVzSGlzdG9yeSksDQogICAgY2xhc3M6IGUuY2xhc3MsDQogICAgY2xhc3NIaXN0b3J5OiBFbmNvdW50ZXJDbGFzc0hpc3RvcnkoZS5jbGFzc0hpc3RvcnkpLA0KICAgIHR5cGU6IGUudHlwZSwNCiAgICBzZXJ2aWNlVHlwZTogZS5zZXJ2aWNlVHlwZSwNCiAgICBwcmlvcml0eTogZS5wcmlvcml0eSwNCiAgICBzdWJqZWN0OiBlLnN1YmplY3QsDQogICAgcGFydGljaXBhbnQ6IEVuY291bnRlclBhcnRpY2lwYW50KGUucGFydGljaXBhbnQpLA0KICAgIHBlcmlvZDogZS5wZXJpb2QsDQogICAgbGVuZ3RoOiBlLmxlbmd0aCwNCiAgICByZWFzb25Db2RlOiBlLnJlYXNvbkNvZGUsDQogICAgcmVhc29uUmVmZXJlbmNlOiBlLnJlYXNvblJlZmVyZW5jZSwNCiAgICBkaWFnbm9zaXM6IEVuY291bnRlckRpYWdub3NpcyhlLmRpYWdub3NpcyksDQogICAgYWNjb3VudDogZS5hY2NvdW50LA0KICAgIGhvc3BpdGFsaXphdGlvbjogRW5jb3VudGVySG9zcGl0YWxpemF0aW9uKGUuaG9zcGl0YWxpemF0aW9uKSwNCiAgICBsb2NhdGlvbjogRW5jb3VudGVyTG9jYXRpb24oZS5sb2NhdGlvbiksDQogICAgcGFydE9mOiBlLnBhcnRPZg0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBPYnNlcnZhdGlvbkxhYkNvZGluZyhjb2RpbmcgTGlzdDxDb2Rpbmc+KToNCiAgY29kaW5nIGMNCiAgcmV0dXJuIENvZGluZ3sNCiAgICBpZDogYy5pZCwNCiAgICBleHRlbnNpb246IGMuZXh0ZW5zaW9uLA0KICAgIHN5c3RlbTogYy5zeXN0ZW0sDQogICAgdmVyc2lvbjogYy52ZXJzaW9uLA0KICAgIGNvZGU6IGMuY29kZSwNCiAgICBkaXNwbGF5OiBjLmRpc3BsYXksDQogICAgdXNlclNlbGVjdGVkOiBjLnVzZXJTZWxlY3RlZA0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBPYnNlcnZhdGlvbkxhYkNhdGVnb3J5KGNhdGVnb3J5IExpc3Q8Q29kZWFibGVDb25jZXB0Pik6DQogIGNhdGVnb3J5IGMNCiAgcmV0dXJuIENvZGVhYmxlQ29uY2VwdHsNCiAgICBjb2Rpbmc6IE9ic2VydmF0aW9uTGFiQ29kaW5nKGMuY29kaW5nKSwNCiAgICB0ZXh0OiBjLnRleHQNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gT2JzZXJ2YXRpb25SZWZlcmVuY2VSYW5nZShyZWZlcmVuY2VSYW5nZSBMaXN0PEZISVIuT2JzZXJ2YXRpb24uUmVmZXJlbmNlUmFuZ2U+KToNCiAgcmVmZXJlbmNlUmFuZ2UgclINCiAgcmV0dXJuIEZISVIuT2JzZXJ2YXRpb24uUmVmZXJlbmNlUmFuZ2V7DQogICAgbG93OiByUi5sb3csDQogICAgaGlnaDogclIuaGlnaCwNCiAgICB0eXBlOiByUi50eXBlLA0KICAgIGFwcGxpZXNUbzogclIuYXBwbGllc1RvLA0KICAgIGFnZTogclIuYWdlLA0KICAgIHRleHQ6IHJSLnRleHQNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gT2JzZXJ2YXRpb25Db21wb25lbnQoY29tcG9uZW50IExpc3Q8RkhJUi5PYnNlcnZhdGlvbi5Db21wb25lbnQ+KToNCiAgY29tcG9uZW50IGMNCiAgcmV0dXJuIEZISVIuT2JzZXJ2YXRpb24uQ29tcG9uZW50ew0KICAgIGNvZGU6IGMuY29kZSwNCiAgICB2YWx1ZTogYy52YWx1ZSwNCiAgICBkYXRhQWJzZW50UmVhc29uOiBjLmRhdGFBYnNlbnRSZWFzb24sDQogICAgaW50ZXJwcmV0YXRpb246IGMuaW50ZXJwcmV0YXRpb24sDQogICAgcmVmZXJlbmNlUmFuZ2U6IGMucmVmZXJlbmNlUmFuZ2UNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gT2JzZXJ2YXRpb25MYWJSZXNvdXJjZShvYnNlcnZhdGlvbiBPYnNlcnZhdGlvbiwgcHJvZmlsZVVSTHMgTGlzdDxGSElSLmNhbm9uaWNhbD4pOg0KICBvYnNlcnZhdGlvbiBvDQogIHJldHVybiBPYnNlcnZhdGlvbnsNCiAgICBpZDogRkhJUi5pZCB7dmFsdWU6ICdMQ1ItJyArIG8uaWR9LA0KICAgIG1ldGE6IE1ldGFFbGVtZW50KG8sIHByb2ZpbGVVUkxzKSwNCiAgICBleHRlbnNpb246IG8uZXh0ZW5zaW9uLA0KICAgIGJhc2VkT246IG8uYmFzZWRPbiwNCiAgICBwYXJ0T2Y6IG8ucGFydE9mLA0KICAgIHN0YXR1czogby5zdGF0dXMsDQogICAgY2F0ZWdvcnk6IE9ic2VydmF0aW9uTGFiQ2F0ZWdvcnkoby5jYXRlZ29yeSksDQogICAgY29kZTogby5jb2RlLA0KICAgIHN1YmplY3Q6IG8uc3ViamVjdCwNCiAgICBmb2N1czogby5mb2N1cywNCiAgICBlbmNvdW50ZXI6IG8uZW5jb3VudGVyLA0KICAgIGVmZmVjdGl2ZTogby5lZmZlY3RpdmUsDQogICAgaXNzdWVkOiBvLmlzc3VlZCwNCiAgICBwZXJmb3JtZXI6IG8ucGVyZm9ybWVyLA0KICAgIHZhbHVlOiBvLnZhbHVlLA0KICAgIGRhdGFBYnNlbnRSZWFzb246IG8uZGF0YUFic2VudFJlYXNvbiwNCiAgICBpbnRlcnByZXRhdGlvbjogby5pbnRlcnByZXRhdGlvbiwNCiAgICBub3RlOiBvLm5vdGUsDQogICAgYm9keVNpdGU6IG8uYm9keVNpdGUsDQogICAgbWV0aG9kOiBvLm1ldGhvZCwNCiAgICBzcGVjaW1lbjogby5zcGVjaW1lbiwNCiAgICBkZXZpY2U6IG8uZGV2aWNlLA0KICAgIHJlZmVyZW5jZVJhbmdlOiBPYnNlcnZhdGlvblJlZmVyZW5jZVJhbmdlKG8ucmVmZXJlbmNlUmFuZ2UpLA0KICAgIGhhc01lbWJlcjogby5oYXNNZW1iZXIsDQogICAgZGVyaXZlZEZyb206IG8uZGVyaXZlZEZyb20sDQogICAgY29tcG9uZW50OiBPYnNlcnZhdGlvbkNvbXBvbmVudChvLmNvbXBvbmVudCkNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gTG9jYXRpb25BZGRyZXNzKGFkZHJlc3MgRkhJUi5BZGRyZXNzKToNCiAgYWRkcmVzcyBhDQogIHJldHVybiBGSElSLkFkZHJlc3N7DQogICAgdXNlOiBhLnVzZSwNCiAgICB0eXBlOiBhLnR5cGUsDQogICAgdGV4dDogYS50ZXh0LA0KICAgIGxpbmU6IGEubGluZSwNCiAgICBjaXR5OiBhLmNpdHksDQogICAgZGlzdHJpY3Q6IGEuZGlzdHJpY3QsDQogICAgc3RhdGU6IGEuc3RhdGUsDQogICAgcG9zdGFsQ29kZTogYS5wb3N0YWxDb2RlLA0KICAgIGNvdW50cnk6IGEuY291bnRyeSwNCiAgICBwZXJpb2Q6IGEucGVyaW9kDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIExvY2F0aW9uUG9zaXRpb24ocG9zaXRpb24gRkhJUi5Mb2NhdGlvbi5Qb3NpdGlvbik6DQogIHBvc2l0aW9uIHANCiAgcmV0dXJuIEZISVIuTG9jYXRpb24uUG9zaXRpb257DQogICAgbG9uZ2l0dWRlOiBwLmxvbmdpdHVkZSwNCiAgICBsYXRpdHVkZTogcC5sYXRpdHVkZSwNCiAgICBhbHRpdHVkZTogcC5hbHRpdHVkZQ0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBMb2NhdGlvbkhvdXJzT2ZPcGVyYXRpb24oaG91cnNPZk9wZXJhdGlvbiBMaXN0PEZISVIuTG9jYXRpb24uSG91cnNPZk9wZXJhdGlvbj4pOg0KICBob3Vyc09mT3BlcmF0aW9uIGhPTw0KICByZXR1cm4gRkhJUi5Mb2NhdGlvbi5Ib3Vyc09mT3BlcmF0aW9uew0KICAgIGRheXNPZldlZWs6IGhPTy5kYXlzT2ZXZWVrLA0KICAgIGFsbERheTogaE9PLmFsbERheSwNCiAgICBvcGVuaW5nVGltZTogaE9PLm9wZW5pbmdUaW1lLA0KICAgIGNsb3NpbmdUaW1lOiBoT08uY2xvc2luZ1RpbWUNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gTG9jYXRpb25SZXNvdXJjZShsb2NhdGlvbiBMb2NhdGlvbiwgcHJvZmlsZVVSTHMgTGlzdDxGSElSLmNhbm9uaWNhbD4pOg0KICBsb2NhdGlvbiBsDQogIHJldHVybiBMb2NhdGlvbnsNCiAgICBpZDogRkhJUi5pZCB7dmFsdWU6ICdMQ1ItJyArIGwuaWR9LA0KICAgIG1ldGE6IE1ldGFFbGVtZW50KGwsIHByb2ZpbGVVUkxzKSwNCiAgICBleHRlbnNpb246IGwuZXh0ZW5zaW9uLA0KICAgIHN0YXR1czogbC5zdGF0dXMsDQogICAgb3BlcmF0aW9uYWxTdGF0dXM6IGwub3BlcmF0aW9uYWxTdGF0dXMsDQogICAgbmFtZTogbC5uYW1lLA0KICAgIGFsaWFzOiBsLmFsaWFzLA0KICAgIGRlc2NyaXB0aW9uOiBsLmRlc2NyaXB0aW9uLA0KICAgIG1vZGU6IGwubW9kZSwNCiAgICB0eXBlOiBsLnR5cGUsDQogICAgdGVsZWNvbTogbC50ZWxlY29tLA0KICAgIGFkZHJlc3M6IExvY2F0aW9uQWRkcmVzcyhsLmFkZHJlc3MpLA0KICAgIHBoeXNpY2FsVHlwZTogbC5waHlzaWNhbFR5cGUsDQogICAgcG9zaXRpb246IExvY2F0aW9uUG9zaXRpb24obC5wb3NpdGlvbiksDQogICAgbWFuYWdpbmdPcmdhbml6YXRpb246IGwubWFuYWdpbmdPcmdhbml6YXRpb24sDQogICAgcGFydE9mOiBsLnBhcnRPZiwNCiAgICBob3Vyc09mT3BlcmF0aW9uOiBMb2NhdGlvbkhvdXJzT2ZPcGVyYXRpb24obC5ob3Vyc09mT3BlcmF0aW9uKSwNCiAgICBhdmFpbGFiaWxpdHlFeGNlcHRpb25zOiBsLmF2YWlsYWJpbGl0eUV4Y2VwdGlvbnMsDQogICAgZW5kcG9pbnQ6IGwuZW5kcG9pbnQNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gTWVkaWNhdGlvbkluZ3JlZGllbnQoaW5ncmVkaWVudCBMaXN0PEZISVIuTWVkaWNhdGlvbi5JbmdyZWRpZW50Pik6DQogIGluZ3JlZGllbnQgaQ0KICByZXR1cm4gRkhJUi5NZWRpY2F0aW9uLkluZ3JlZGllbnR7DQogICAgaXRlbTogaS5pdGVtLA0KICAgIHN0cmVuZ3RoOiBpLnN0cmVuZ3RoDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIE1lZGljYXRpb25CYXRjaChiYXRjaCBGSElSLk1lZGljYXRpb24uQmF0Y2gpOg0KICBiYXRjaCBiDQogIHJldHVybiBGSElSLk1lZGljYXRpb24uQmF0Y2h7DQogICAgbG90TnVtYmVyOiBiLmxvdE51bWJlciwNCiAgICBleHBpcmF0aW9uRGF0ZTogYi5leHBpcmF0aW9uRGF0ZQ0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBNZWRpY2F0aW9uUmVzb3VyY2UobWVkaWNhdGlvbiBNZWRpY2F0aW9uLCBwcm9maWxlVVJMcyBMaXN0PEZISVIuY2Fub25pY2FsPik6DQogIG1lZGljYXRpb24gbQ0KICByZXR1cm4gTWVkaWNhdGlvbnsNCiAgICBpZDogRkhJUi5pZCB7dmFsdWU6ICdMQ1ItJyArIG0uaWR9LA0KICAgIG1ldGE6IE1ldGFFbGVtZW50KG0sIHByb2ZpbGVVUkxzKSwNCiAgICBleHRlbnNpb246IG0uZXh0ZW5zaW9uLA0KICAgIGNvZGU6IG0uY29kZSwNCiAgICBzdGF0dXM6IG0uc3RhdHVzLA0KICAgIG1hbnVmYWN0dXJlcjogbS5tYW51ZmFjdHVyZXIsDQogICAgZm9ybTogbS5mb3JtLA0KICAgIGFtb3VudDogbS5hbW91bnQsDQogICAgaW5ncmVkaWVudDogTWVkaWNhdGlvbkluZ3JlZGllbnQobS5pbmdyZWRpZW50KSwNCiAgICBiYXRjaDogTWVkaWNhdGlvbkJhdGNoKG0uYmF0Y2gpDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIE1lZGljYXRpb25BZG1pbmlzdHJhdGlvblBlcmZvcm1lcihwZXJmb3JtZXIgTGlzdDxGSElSLk1lZGljYXRpb25BZG1pbmlzdHJhdGlvbi5QZXJmb3JtZXI+KToNCiAgcGVyZm9ybWVyIHANCiAgcmV0dXJuIEZISVIuTWVkaWNhdGlvbkFkbWluaXN0cmF0aW9uLlBlcmZvcm1lcnsNCiAgICBmdW5jdGlvbjogcC5mdW5jdGlvbiwNCiAgICBhY3RvcjogcC5hY3Rvcg0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBNZWRpY2F0aW9uQWRtaW5pc3RyYXRpb25Eb3NhZ2UoZG9zYWdlIEZISVIuTWVkaWNhdGlvbkFkbWluaXN0cmF0aW9uLkRvc2FnZSk6DQogIGRvc2FnZSBkDQogIHJldHVybiBGSElSLk1lZGljYXRpb25BZG1pbmlzdHJhdGlvbi5Eb3NhZ2V7DQogICAgdGV4dDogZC50ZXh0LA0KICAgIHNpdGU6IGQuc2l0ZSwNCiAgICByb3V0ZTogZC5yb3V0ZSwNCiAgICBtZXRob2Q6IGQubWV0aG9kLA0KICAgIGRvc2U6IGQuZG9zZSwNCiAgICByYXRlOiBkLnJhdGUNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gTWVkaWNhdGlvbkFkbWluaXN0cmF0aW9uUmVzb3VyY2UobWVkaWNhdGlvbkFkbWluaXN0cmF0aW9uIE1lZGljYXRpb25BZG1pbmlzdHJhdGlvbiwgcHJvZmlsZVVSTHMgTGlzdDxGSElSLmNhbm9uaWNhbD4pOg0KICBtZWRpY2F0aW9uQWRtaW5pc3RyYXRpb24gbQ0KICByZXR1cm4gTWVkaWNhdGlvbkFkbWluaXN0cmF0aW9uew0KICAgIGlkOiBGSElSLmlkIHt2YWx1ZTogJ0xDUi0nICsgbS5pZH0sDQogICAgbWV0YTogTWV0YUVsZW1lbnQobSwgcHJvZmlsZVVSTHMpLA0KICAgIGV4dGVuc2lvbjogbS5leHRlbnNpb24sDQogICAgaW5zdGFudGlhdGVzOiBtLmluc3RhbnRpYXRlcywNCiAgICBwYXJ0T2Y6IG0ucGFydE9mLA0KICAgIHN0YXR1czogbS5zdGF0dXMsDQogICAgc3RhdHVzUmVhc29uOiBtLnN0YXR1c1JlYXNvbiwNCiAgICBjYXRlZ29yeTogbS5jYXRlZ29yeSwNCiAgICBtZWRpY2F0aW9uOiBtLm1lZGljYXRpb24sDQogICAgc3ViamVjdDogbS5zdWJqZWN0LA0KICAgIGNvbnRleHQ6IG0uY29udGV4dCwNCiAgICBzdXBwb3J0aW5nSW5mb3JtYXRpb246IG0uc3VwcG9ydGluZ0luZm9ybWF0aW9uLA0KICAgIGVmZmVjdGl2ZTogbS5lZmZlY3RpdmUsDQogICAgcGVyZm9ybWVyOiBNZWRpY2F0aW9uQWRtaW5pc3RyYXRpb25QZXJmb3JtZXIobS5wZXJmb3JtZXIpLA0KICAgIHJlYXNvbkNvZGU6IG0ucmVhc29uQ29kZSwNCiAgICByZWFzb25SZWZlcmVuY2U6IG0ucmVhc29uUmVmZXJlbmNlLA0KICAgIHJlcXVlc3Q6IG0ucmVxdWVzdCwNCiAgICBkZXZpY2U6IG0uZGV2aWNlLA0KICAgIG5vdGU6IG0ubm90ZSwNCiAgICBkb3NhZ2U6IE1lZGljYXRpb25BZG1pbmlzdHJhdGlvbkRvc2FnZShtLmRvc2FnZSksDQogICAgZXZlbnRIaXN0b3J5OiBtLmV2ZW50SGlzdG9yeQ0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBNZWRpY2F0aW9uUmVxdWVzdERvc2VBbmRSYXRlKGRvc2VBbmRSYXRlIExpc3Q8RkhJUi5Eb3NhZ2UuRG9zZUFuZFJhdGU+KToNCiAgZG9zZUFuZFJhdGUgZFINCiAgcmV0dXJuIEZISVIuRG9zYWdlLkRvc2VBbmRSYXRlew0KICAgIHR5cGU6IGRSLnR5cGUsDQogICAgZG9zZTogZFIuZG9zZSwNCiAgICByYXRlOiBkUi5yYXRlDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIE1lZGljYXRpb25SZXF1ZXN0RG9zYWdlSW5zdHJ1Y3Rpb24oZG9zYWdlSW5zdHJ1Y3Rpb24gTGlzdDxGSElSLkRvc2FnZT4pOg0KICBkb3NhZ2VJbnN0cnVjdGlvbiBkSQ0KICByZXR1cm4gRkhJUi5Eb3NhZ2V7DQogICAgdGV4dDogZEkudGV4dCwNCiAgICBwYXRpZW50SW5zdHJ1Y3Rpb246IGRJLnBhdGllbnRJbnN0cnVjdGlvbiwNCiAgICB0aW1pbmc6IGRJLnRpbWluZywNCiAgICBhc05lZWRlZDogZEkuYXNOZWVkZWQsDQogICAgc2l0ZTogZEkuc2l0ZSwNCiAgICByb3V0ZTogZEkucm91dGUsDQogICAgbWV0aG9kOiBkSS5tZXRob2QsDQogICAgZG9zZUFuZFJhdGU6IE1lZGljYXRpb25SZXF1ZXN0RG9zZUFuZFJhdGUoZEkuZG9zZUFuZFJhdGUpDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIE1lZGljYXRpb25SZXF1ZXN0UmVzb3VyY2UobWVkaWNhdGlvblJlcXVlc3QgTWVkaWNhdGlvblJlcXVlc3QsIHByb2ZpbGVVUkxzIExpc3Q8RkhJUi5jYW5vbmljYWw+KToNCiAgbWVkaWNhdGlvblJlcXVlc3QgbQ0KICByZXR1cm4gTWVkaWNhdGlvblJlcXVlc3R7DQogICAgaWQ6IEZISVIuaWQge3ZhbHVlOiAnTENSLScgKyBtLmlkfSwNCiAgICBtZXRhOiBNZXRhRWxlbWVudChtZWRpY2F0aW9uUmVxdWVzdCwgcHJvZmlsZVVSTHMpLA0KICAgIGV4dGVuc2lvbjogbS5leHRlbnNpb24sDQogICAgc3RhdHVzOiBtLnN0YXR1cywNCiAgICBzdGF0dXNSZWFzb246IG0uc3RhdHVzUmVhc29uLA0KICAgIGludGVudDogbS5pbnRlbnQsDQogICAgY2F0ZWdvcnk6IG0uY2F0ZWdvcnksDQogICAgcHJpb3JpdHk6IG0ucHJpb3JpdHksDQogICAgZG9Ob3RQZXJmb3JtOiBtLmRvTm90UGVyZm9ybSwNCiAgICByZXBvcnRlZDogbS5yZXBvcnRlZCwNCiAgICBtZWRpY2F0aW9uOiBtLm1lZGljYXRpb24sDQogICAgc3ViamVjdDogbS5zdWJqZWN0LA0KICAgIGVuY291bnRlcjogbS5lbmNvdW50ZXIsDQogICAgYXV0aG9yZWRPbjogbS5hdXRob3JlZE9uLA0KICAgIHJlcXVlc3RlcjogbS5yZXF1ZXN0ZXIsDQogICAgcmVjb3JkZXI6IG0ucmVjb3JkZXIsDQogICAgcmVhc29uQ29kZTogbS5yZWFzb25Db2RlLA0KICAgIHJlYXNvblJlZmVyZW5jZTogbS5yZWFzb25SZWZlcmVuY2UsDQogICAgaW5zdGFudGlhdGVzQ2Fub25pY2FsOiBtLmluc3RhbnRpYXRlc0Nhbm9uaWNhbCwNCiAgICBpbnN0YW50aWF0ZXNVcmk6IG0uaW5zdGFudGlhdGVzVXJpLA0KICAgIGNvdXJzZU9mVGhlcmFweVR5cGU6IG0uY291cnNlT2ZUaGVyYXB5VHlwZSwNCiAgICBkb3NhZ2VJbnN0cnVjdGlvbjogTWVkaWNhdGlvblJlcXVlc3REb3NhZ2VJbnN0cnVjdGlvbihtLmRvc2FnZUluc3RydWN0aW9uKQ0KICB9DQoNCi8qIE5vIGxvbmdlciBuZWVkZWQgYnV0IHNhdmluZyBpbiBjYXNlIGl0J3MgdXNlZnVsIGxhdGVyDQpkZWZpbmUgZnVuY3Rpb24gUGF0aWVudElkZW50aWZpZXIoaWRlbnRpZmllciBMaXN0PEZISVIuSWRlbnRpZmllcj4pOg0KICBpZGVudGlmaWVyIGkNCiAgcmV0dXJuIEZISVIuSWRlbnRpZmllcnsNCiAgICBpZDogaS5pZCwNCiAgICBleHRlbnNpb246IGkuZXh0ZW5zaW9uLA0KICAgIHVzZTogaS51c2UsDQogICAgdHlwZTogaS50eXBlLA0KICAgIHN5c3RlbTogaS5zeXN0ZW0sDQogICAgdmFsdWU6IGkudmFsdWUsDQogICAgcGVyaW9kOiBpLnBlcmlvZCwNCiAgICBhc3NpZ25lcjogaS5hc3NpZ25lcg0KICB9Ki8NCg0KZGVmaW5lIGZ1bmN0aW9uIFBhdGllbnROYW1lKG5hbWUgTGlzdDxGSElSLkh1bWFuTmFtZT4pOg0KICBuYW1lIG4NCiAgcmV0dXJuIEZISVIuSHVtYW5OYW1lew0KICAgIGlkOiBuLmlkLA0KICAgIGV4dGVuc2lvbjogbi5leHRlbnNpb24sDQogICAgdXNlOiBuLnVzZSwNCiAgICB0ZXh0OiBuLnRleHQsDQogICAgZmFtaWx5OiBuLmZhbWlseSwNCiAgICBnaXZlbjogbi5naXZlbiwNCiAgICBwcmVmaXg6IG4ucHJlZml4LA0KICAgIHN1ZmZpeDogbi5zdWZmaXgsDQogICAgcGVyaW9kOiBuLnBlcmlvZA0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBQYXRpZW50VGVsZWNvbSh0ZWxlY29tIExpc3Q8RkhJUi5Db250YWN0UG9pbnQ+KToNCiAgdGVsZWNvbSB0DQogIHJldHVybiBGSElSLkNvbnRhY3RQb2ludHsNCiAgICBzeXN0ZW06IHQuc3lzdGVtLA0KICAgIHZhbHVlOiB0LnZhbHVlLA0KICAgIHVzZTogdC51c2UsDQogICAgcmFuazogdC5yYW5rLA0KICAgIHBlcmlvZDogdC5wZXJpb2QNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gUGF0aWVudEFkZHJlc3MoYWRkcmVzcyBMaXN0PEZISVIuQWRkcmVzcz4pOg0KICBhZGRyZXNzIGENCiAgcmV0dXJuIEZISVIuQWRkcmVzc3sNCiAgICBpZDogYS5pZCwNCiAgICBleHRlbnNpb246IGEuZXh0ZW5zaW9uLA0KICAgIHVzZTogYS51c2UsDQogICAgdHlwZTogYS50eXBlLA0KICAgIHRleHQ6IGEudGV4dCwNCiAgICBsaW5lOiBhLmxpbmUsDQogICAgY2l0eTogYS5jaXR5LA0KICAgIGRpc3RyaWN0OiBhLmRpc3RyaWN0LA0KICAgIHN0YXRlOiBhLnN0YXRlLA0KICAgIHBvc3RhbENvZGU6IGEucG9zdGFsQ29kZSwNCiAgICBjb3VudHJ5OiBhLmNvdW50cnksDQogICAgcGVyaW9kOiBhLnBlcmlvZA0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBQYXRpZW50Q29udGFjdChjb250YWN0IExpc3Q8RkhJUi5QYXRpZW50LkNvbnRhY3Q+KToNCiAgY29udGFjdCBjDQogIHJldHVybiBGSElSLlBhdGllbnQuQ29udGFjdHsNCiAgICBpZDogYy5pZCwNCiAgICBleHRlbnNpb246IGMuZXh0ZW5zaW9uLA0KICAgIHJlbGF0aW9uc2hpcDogYy5yZWxhdGlvbnNoaXAsDQogICAgbmFtZTogYy5uYW1lLA0KICAgIHRlbGVjb206IGMudGVsZWNvbSwNCiAgICBhZGRyZXNzOiBjLmFkZHJlc3MsDQogICAgZ2VuZGVyOiBjLmdlbmRlciwNCiAgICBvcmdhbml6YXRpb246IGMub3JnYW5pemF0aW9uLA0KICAgIHBlcmlvZDogYy5wZXJpb2QNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gUGF0aWVudENvbW11bmljYXRpb24oY29tbXVuaWNhdGlvbiBMaXN0PEZISVIuUGF0aWVudC5Db21tdW5pY2F0aW9uPik6DQogIGNvbW11bmljYXRpb24gYw0KICByZXR1cm4gRkhJUi5QYXRpZW50LkNvbW11bmljYXRpb257DQogICAgaWQ6IGMuaWQsDQogICAgZXh0ZW5zaW9uOiBjLmV4dGVuc2lvbiwNCiAgICBsYW5ndWFnZTogYy5sYW5ndWFnZSwNCiAgICBwcmVmZXJyZWQ6IGMucHJlZmVycmVkDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIFBhdGllbnRMaW5rKGxpbmsgTGlzdDxGSElSLlBhdGllbnQuTGluaz4pOg0KICBsaW5rIGwNCiAgcmV0dXJuIEZISVIuUGF0aWVudC5MaW5rew0KICAgIGlkOiBsLmlkLA0KICAgIGV4dGVuc2lvbjogbC5leHRlbnNpb24sDQogICAgbW9kaWZpZXJFeHRlbnNpb246IGwubW9kaWZpZXJFeHRlbnNpb24sDQogICAgb3RoZXI6IGwub3RoZXIsDQogICAgdHlwZTogbC50eXBlDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIFBhdGllbnRSZXNvdXJjZShwYXRpZW50IFBhdGllbnQsIHByb2ZpbGVVUkxzIExpc3Q8RkhJUi5jYW5vbmljYWw+KToNCiAgcGF0aWVudCBwDQogIHJldHVybiBQYXRpZW50ew0KICAgIGlkOiBGSElSLmlke3ZhbHVlOiAnTENSLScgKyBwLmlkfSwNCiAgICBtZXRhOiBNZXRhRWxlbWVudChwLCBwcm9maWxlVVJMcyksDQogICAgZXh0ZW5zaW9uOiBHZXRQYXRpZW50RXh0ZW5zaW9ucyhwKSB1bmlvbiBHZXRJZEV4dGVuc2lvbnMocCksDQogICAgaWRlbnRpZmllcjogcC5pZGVudGlmaWVyLA0KICAgIGFjdGl2ZTogcC5hY3RpdmUsDQogICAgbmFtZTogUGF0aWVudE5hbWUocC5uYW1lKSwNCiAgICB0ZWxlY29tOiBQYXRpZW50VGVsZWNvbShwLnRlbGVjb20pLA0KICAgIGdlbmRlcjogcC5nZW5kZXIsDQogICAgYmlydGhEYXRlOiBwLmJpcnRoRGF0ZSwNCiAgICBkZWNlYXNlZDogcC5kZWNlYXNlZCwNCiAgICBhZGRyZXNzOiBQYXRpZW50QWRkcmVzcyhwLmFkZHJlc3MpLA0KICAgIG1hcml0YWxTdGF0dXM6IHAubWFyaXRhbFN0YXR1cywNCiAgICBtdWx0aXBsZUJpcnRoOiBwLm11bHRpcGxlQmlydGgsDQogICAgcGhvdG86IHAucGhvdG8sDQogICAgY29udGFjdDogUGF0aWVudENvbnRhY3QocC5jb250YWN0KSwNCiAgICBjb21tdW5pY2F0aW9uOiBQYXRpZW50Q29tbXVuaWNhdGlvbihwLmNvbW11bmljYXRpb24pLA0KICAgIGdlbmVyYWxQcmFjdGl0aW9uZXI6IHAuZ2VuZXJhbFByYWN0aXRpb25lciwNCiAgICBtYW5hZ2luZ09yZ2FuaXphdGlvbjogcC5tYW5hZ2luZ09yZ2FuaXphdGlvbiwNCiAgICBsaW5rOiBQYXRpZW50TGluayhwLmxpbmspDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIFByb2NlZHVyZVBlcmZvcm1lcihwZXJmb3JtZXIgTGlzdDxGSElSLlByb2NlZHVyZS5QZXJmb3JtZXI+KToNCiAgcGVyZm9ybWVyIHANCiAgcmV0dXJuIEZISVIuUHJvY2VkdXJlLlBlcmZvcm1lcnsNCiAgICBmdW5jdGlvbjogcC5mdW5jdGlvbiwNCiAgICBhY3RvcjogcC5hY3RvciwNCiAgICBvbkJlaGFsZk9mOiBwLm9uQmVoYWxmT2YNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gUHJvY2VkdXJlRm9jYWxEZXZpY2UoZGV2aWNlIExpc3Q8RkhJUi5Qcm9jZWR1cmUuRm9jYWxEZXZpY2U+KToNCiAgZGV2aWNlIGQNCiAgcmV0dXJuIEZISVIuUHJvY2VkdXJlLkZvY2FsRGV2aWNlew0KICAgIGFjdGlvbjogZC5hY3Rpb24sDQogICAgbWFuaXB1bGF0ZWQ6IGQubWFuaXB1bGF0ZWQNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gUHJvY2VkdXJlUmVzb3VyY2UocHJvY2VkdXJlIFByb2NlZHVyZSwgcHJvZmlsZVVSTHMgTGlzdDxGSElSLmNhbm9uaWNhbD4pOg0KICBwcm9jZWR1cmUgcA0KICByZXR1cm4gUHJvY2VkdXJlew0KICAgIGlkOiBGSElSLmlkIHt2YWx1ZTogJ0xDUi0nICsgcC5pZH0sDQogICAgbWV0YTogTWV0YUVsZW1lbnQocCwgcHJvZmlsZVVSTHMpLA0KICAgIGV4dGVuc2lvbjogcC5leHRlbnNpb24sDQogICAgaW5zdGFudGlhdGVzQ2Fub25pY2FsOiBwLmluc3RhbnRpYXRlc0Nhbm9uaWNhbCwNCiAgICBpbnN0YW50aWF0ZXNVcmk6IHAuaW5zdGFudGlhdGVzVXJpLA0KICAgIGJhc2VkT246IHAuYmFzZWRPbiwNCiAgICBwYXJ0T2Y6IHAucGFydE9mLA0KICAgIHN0YXR1czogcC5zdGF0dXMsDQogICAgc3RhdHVzUmVhc29uOiBwLnN0YXR1c1JlYXNvbiwNCiAgICBjYXRlZ29yeTogcC5jYXRlZ29yeSwNCiAgICBjb2RlOiBwLmNvZGUsDQogICAgc3ViamVjdDogcC5zdWJqZWN0LA0KICAgIGVuY291bnRlcjogcC5lbmNvdW50ZXIsDQogICAgcGVyZm9ybWVkOiBwLnBlcmZvcm1lZCwNCiAgICByZWNvcmRlcjogcC5yZWNvcmRlciwNCiAgICBhc3NlcnRlcjogcC5hc3NlcnRlciwNCiAgICBwZXJmb3JtZXI6IFByb2NlZHVyZVBlcmZvcm1lcihwLnBlcmZvcm1lciksDQogICAgbG9jYXRpb246IHAubG9jYXRpb24sDQogICAgcmVhc29uQ29kZTogcC5yZWFzb25Db2RlLA0KICAgIHJlYXNvblJlZmVyZW5jZTogcC5yZWFzb25SZWZlcmVuY2UsDQogICAgYm9keVNpdGU6IHAuYm9keVNpdGUsDQogICAgb3V0Y29tZTogcC5vdXRjb21lLA0KICAgIHJlcG9ydDogcC5yZXBvcnQsDQogICAgY29tcGxpY2F0aW9uOiBwLmNvbXBsaWNhdGlvbiwNCiAgICBjb21wbGljYXRpb25EZXRhaWw6IHAuY29tcGxpY2F0aW9uRGV0YWlsLA0KICAgIGZvbGxvd1VwOiBwLmZvbGxvd1VwLA0KICAgIG5vdGU6IHAubm90ZSwNCiAgICBmb2NhbERldmljZTogUHJvY2VkdXJlRm9jYWxEZXZpY2UocC5mb2NhbERldmljZSksDQogICAgdXNlZFJlZmVyZW5jZTogcC51c2VkUmVmZXJlbmNlLA0KICAgIHVzZWRDb2RlOiBwLnVzZWRDb2RlDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIFNlcnZpY2VSZXF1ZXN0UmVzb3VyY2Uoc2VydmljZVJlcXVlc3QgU2VydmljZVJlcXVlc3QsIHByb2ZpbGVVUkxzIExpc3Q8RkhJUi5jYW5vbmljYWw+KToNCiAgc2VydmljZVJlcXVlc3Qgc1INCiAgcmV0dXJuIFNlcnZpY2VSZXF1ZXN0ew0KICAgIGlkOiBGSElSLmlkIHt2YWx1ZTogJ0xDUi0nICsgc1IuaWR9LA0KICAgIG1ldGE6IE1ldGFFbGVtZW50KHNSLCBwcm9maWxlVVJMcyksDQogICAgZXh0ZW5zaW9uOiBzUi5leHRlbnNpb24sDQogICAgaW5zdGFudGlhdGVzQ2Fub25pY2FsOiBzUi5pbnN0YW50aWF0ZXNDYW5vbmljYWwsDQogICAgaW5zdGFudGlhdGVzVXJpOiBzUi5pbnN0YW50aWF0ZXNVcmksDQogICAgYmFzZWRPbjogc1IuYmFzZWRPbiwNCiAgICByZXBsYWNlczogc1IucmVwbGFjZXMsDQogICAgcmVxdWlzaXRpb246IHNSLnJlcXVpc2l0aW9uLA0KICAgIHN0YXR1czogc1Iuc3RhdHVzLA0KICAgIGludGVudDogc1IuaW50ZW50LA0KICAgIGNhdGVnb3J5OiBzUi5jYXRlZ29yeSwNCiAgICBwcmlvcml0eTogc1IucHJpb3JpdHksDQogICAgZG9Ob3RQZXJmb3JtOiBzUi5kb05vdFBlcmZvcm0sDQogICAgY29kZTogc1IuY29kZSwNCiAgICBvcmRlckRldGFpbDogc1Iub3JkZXJEZXRhaWwsDQogICAgcXVhbnRpdHk6IHNSLnF1YW50aXR5LA0KICAgIHN1YmplY3Q6IHNSLnN1YmplY3QsDQogICAgZW5jb3VudGVyOiBzUi5lbmNvdW50ZXIsDQogICAgb2NjdXJyZW5jZTogc1Iub2NjdXJyZW5jZSwNCiAgICBhc05lZWRlZDogc1IuYXNOZWVkZWQsDQogICAgYXV0aG9yZWRPbjogc1IuYXV0aG9yZWRPbiwNCiAgICByZXF1ZXN0ZXI6IHNSLnJlcXVlc3RlciwNCiAgICBwZXJmb3JtZXJUeXBlOiBzUi5wZXJmb3JtZXJUeXBlLA0KICAgIHBlcmZvcm1lcjogc1IucGVyZm9ybWVyLA0KICAgIGxvY2F0aW9uQ29kZTogc1IubG9jYXRpb25Db2RlLA0KICAgIGxvY2F0aW9uUmVmZXJlbmNlOiBzUi5sb2NhdGlvblJlZmVyZW5jZSwNCiAgICByZWFzb25Db2RlOiBzUi5yZWFzb25Db2RlLA0KICAgIHJlYXNvblJlZmVyZW5jZTogc1IucmVhc29uUmVmZXJlbmNlLA0KICAgIGluc3VyYW5jZTogc1IuaW5zdXJhbmNlLA0KICAgIHN1cHBvcnRpbmdJbmZvOiBzUi5zdXBwb3J0aW5nSW5mbywNCiAgICBzcGVjaW1lbjogc1Iuc3BlY2ltZW4sDQogICAgYm9keVNpdGU6IHNSLmJvZHlTaXRlLA0KICAgIG5vdGU6IHNSLm5vdGUsDQogICAgcGF0aWVudEluc3RydWN0aW9uOiBzUi5wYXRpZW50SW5zdHJ1Y3Rpb24sDQogICAgcmVsZXZhbnRIaXN0b3J5OiBzUi5yZWxldmFudEhpc3RvcnkNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gU3BlY2ltZW5Db2xsZWN0aW9uKGNvbGxlY3Rpb24gRkhJUi5TcGVjaW1lbi5Db2xsZWN0aW9uKToNCiAgY29sbGVjdGlvbiBjDQogIHJldHVybiBGSElSLlNwZWNpbWVuLkNvbGxlY3Rpb257DQogICAgY29sbGVjdG9yOiBjLmNvbGxlY3RvciwNCiAgICBjb2xsZWN0ZWQ6IGMuY29sbGVjdGVkLA0KICAgIC8vZHVyYXRpb246IGMuZHVyYXRpb24sIERvZXMgbm90IHBhcnNlIGZvciBzb21lIHJlYXNvbj8gTmVlZCB0byBicmluZyB1cCB3aXRoIFNtaWxlQ0RSDQogICAgcXVhbnRpdHk6IGMucXVhbnRpdHksDQogICAgbWV0aG9kOiBjLm1ldGhvZCwNCiAgICBib2R5U2l0ZTogYy5ib2R5U2l0ZSwNCiAgICBmYXN0aW5nU3RhdHVzOiBjLmZhc3RpbmdTdGF0dXMNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gU3BlY2ltZW5Qcm9jZXNzaW5nKHByb2Nlc3NpbmcgTGlzdDxGSElSLlNwZWNpbWVuLlByb2Nlc3Npbmc+KToNCiAgcHJvY2Vzc2luZyBwDQogIHJldHVybiBGSElSLlNwZWNpbWVuLlByb2Nlc3Npbmd7DQogICAgZGVzY3JpcHRpb246IHAuZGVzY3JpcHRpb24sDQogICAgcHJvY2VkdXJlOiBwLnByb2NlZHVyZSwNCiAgICBhZGRpdGl2ZTogcC5hZGRpdGl2ZSwNCiAgICB0aW1lOiBwLnRpbWUNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gU3BlY2ltZW5Db250YWluZXIoY29udGFpbmVyIExpc3Q8RkhJUi5TcGVjaW1lbi5Db250YWluZXI+KToNCiAgY29udGFpbmVyIGMNCiAgcmV0dXJuIEZISVIuU3BlY2ltZW4uQ29udGFpbmVyew0KICAgIGRlc2NyaXB0aW9uOiBjLmRlc2NyaXB0aW9uLA0KICAgIHR5cGU6IGMudHlwZSwNCiAgICBjYXBhY2l0eTogYy5jYXBhY2l0eSwNCiAgICBzcGVjaW1lblF1YW50aXR5OiBjLnNwZWNpbWVuUXVhbnRpdHksDQogICAgYWRkaXRpdmU6IGMuYWRkaXRpdmUNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gU3BlY2ltZW5SZXNvdXJjZShzcGVjaW1lbiBTcGVjaW1lbiwgcHJvZmlsZVVSTHMgTGlzdDxGSElSLmNhbm9uaWNhbD4pOg0KICBzcGVjaW1lbiBzDQogIHJldHVybiBTcGVjaW1lbnsNCiAgICBpZDogRkhJUi5pZCB7dmFsdWU6ICdMQ1ItJyArIHMuaWR9LA0KICAgIG1ldGE6IE1ldGFFbGVtZW50KHMsIHByb2ZpbGVVUkxzKSwNCiAgICBleHRlbnNpb246IHMuZXh0ZW5zaW9uLA0KICAgIGlkZW50aWZpZXI6IHMuaWRlbnRpZmllciwNCiAgICBhY2Nlc3Npb25JZGVudGlmaWVyOiBzLmFjY2Vzc2lvbklkZW50aWZpZXIsDQogICAgc3RhdHVzOiBzLnN0YXR1cywNCiAgICB0eXBlOiBzLnR5cGUsDQogICAgc3ViamVjdDogcy5zdWJqZWN0LA0KICAgIHJlY2VpdmVkVGltZTogcy5yZWNlaXZlZFRpbWUsDQogICAgcGFyZW50OiBzLnBhcmVudCwNCiAgICByZXF1ZXN0OiBzLnJlcXVlc3QsDQogICAgY29sbGVjdGlvbjogU3BlY2ltZW5Db2xsZWN0aW9uKHMuY29sbGVjdGlvbiksDQogICAgcHJvY2Vzc2luZzogU3BlY2ltZW5Qcm9jZXNzaW5nKHMucHJvY2Vzc2luZyksDQogICAgY29udGFpbmVyOiBTcGVjaW1lbkNvbnRhaW5lcihzLmNvbnRhaW5lciksDQogICAgY29uZGl0aW9uOiBzLmNvbmRpdGlvbiwNCiAgICBub3RlOiBzLm5vdGUNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gIk9wZXJhdGlvbk91dGNvbWVSZXNvdXJjZSIoZXJyb3JJZCBTdHJpbmcsIHJlc291cmNlSWQgRkhJUi5pZCwgbWVzc2FnZSBTdHJpbmcpOg0KICBPcGVyYXRpb25PdXRjb21lew0KICAgICAgaWQ6IEZISVIuaWR7dmFsdWU6IGVycm9ySWR9LA0KICAgICAgaXNzdWU6IHsNCiAgICAgICAgICBGSElSLk9wZXJhdGlvbk91dGNvbWUuSXNzdWV7DQogICAgICAgICAgc2V2ZXJpdHk6IEZISVIuSXNzdWVTZXZlcml0eXt2YWx1ZTogJ2Vycm9yJ30sDQogICAgICAgICAgY29kZTogRkhJUi5Jc3N1ZVR5cGV7dmFsdWU6ICdleGNlcHRpb24nfSwNCiAgICAgICAgICBkZXRhaWxzOiANCiAgICAgICAgICAgICAgRkhJUi5Db2RlYWJsZUNvbmNlcHR7DQogICAgICAgICAgICAgICAgICBjb2Rpbmc6IHsNCiAgICAgICAgICAgICAgICAgICAgICBDb2Rpbmd7DQogICAgICAgICAgICAgICAgICAgICAgc3lzdGVtOiB1cml7dmFsdWU6ICdodHRwczovL2xhbnRhbmFncm91cC5jb20vdmFsaWRhdGlvbi1lcnJvcid9LA0KICAgICAgICAgICAgICAgICAgICAgIGNvZGU6IGNvZGV7dmFsdWU6ICdFcnJvcid9LA0KICAgICAgICAgICAgICAgICAgICAgIGRpc3BsYXk6IHN0cmluZ3t2YWx1ZTogJ1Jlc291cmNlICcgKyByZXNvdXJjZUlkLnZhbHVlICsgJyBmYWlsZWQgdmFsaWRhdGlvbjogJyArIG1lc3NhZ2V9DQogICAgICAgICAgICAgICAgICAgICAgfQ0KICAgICAgICAgICAgICAgICAgfQ0KICAgICAgICAgICAgICB9DQogICAgICAgICAgfQ0KICAgICAgfQ0KICB9"
			}
		  ]
		},
		"request": {
		  "method": "PUT",
		  "url": "Library/SharedResourceCreation"
		}
	  },
	  {
		"resource": {
		  "resourceType": "Library",
		  "id": "FHIRHelpers",
		  "contained": [
			{
			  "resourceType": "Parameters",
			  "id": "options",
			  "parameter": [
				{
				  "name": "translatorVersion",
				  "valueString": "3.5.1"
				},
				{
				  "name": "option",
				  "valueString": "EnableDateRangeOptimization"
				},
				{
				  "name": "option",
				  "valueString": "EnableAnnotations"
				},
				{
				  "name": "option",
				  "valueString": "EnableLocators"
				},
				{
				  "name": "option",
				  "valueString": "DisableListDemotion"
				},
				{
				  "name": "option",
				  "valueString": "DisableListPromotion"
				},
				{
				  "name": "analyzeDataRequirements",
				  "valueBoolean": false
				},
				{
				  "name": "collapseDataRequirements",
				  "valueBoolean": true
				},
				{
				  "name": "compatibilityLevel",
				  "valueString": "1.5"
				},
				{
				  "name": "enableCqlOnly",
				  "valueBoolean": false
				},
				{
				  "name": "errorLevel",
				  "valueString": "Info"
				},
				{
				  "name": "signatureLevel",
				  "valueString": "None"
				},
				{
				  "name": "validateUnits",
				  "valueBoolean": true
				},
				{
				  "name": "verifyOnly",
				  "valueBoolean": false
				}
			  ]
			}
		  ],
		  "extension": [
			{
			  "url": "http://hl7.org/fhir/StructureDefinition/cqf-cqlOptions",
			  "valueReference": {
				"reference": "#options"
			  }
			}
		  ],
		  "url": "http://hl7.org/fhir/Library/FHIRHelpers",
		  "version": "4.0.1",
		  "name": "FHIRHelpers",
		  "status": "draft",
		  "type": {
			"coding": [
			  {
				"system": "http://terminology.hl7.org/CodeSystem/library-type",
				"code": "logic-library"
			  }
			]
		  },
		  "relatedArtifact": [
			{
			  "type": "depends-on",
			  "display": "FHIR model information",
			  "resource": "http://fhir.org/guides/cqf/common/Library/FHIR-ModelInfo|4.0.1"
			}
		  ],
		  "content": [
			{
			  "contentType": "text/cql",
			  "data": "LyoNCkBhdXRob3I6IEJyeW4gUmhvZGVzDQpAZGVzY3JpcHRpb246IFRoaXMgbGlicmFyeSBkZWZpbmVzIGZ1bmN0aW9ucyB0byBjb252ZXJ0IGJldHdlZW4gRkhJUg0KIGRhdGEgdHlwZXMgYW5kIENRTCBzeXN0ZW0tZGVmaW5lZCB0eXBlcywgYXMgd2VsbCBhcyBmdW5jdGlvbnMgdG8gc3VwcG9ydA0KIEZISVJQYXRoIGltcGxlbWVudGF0aW9uLiBGb3IgbW9yZSBpbmZvcm1hdGlvbiwgc2VlIHRoZSBGSElSSGVscGVycyB3aWtpIHBhZ2U6DQogaHR0cHM6Ly9naXRodWIuY29tL2NxZnJhbWV3b3JrL2NsaW5pY2FsX3F1YWxpdHlfbGFuZ3VhZ2Uvd2lraS9GSElSSGVscGVycw0KQGFsbG93Rmx1ZW50OiB0cnVlDQoqLw0KbGlicmFyeSBGSElSSGVscGVycyB2ZXJzaW9uICc0LjAuMScNCg0KdXNpbmcgRkhJUiB2ZXJzaW9uICc0LjAuMScNCg0KZGVmaW5lIGZ1bmN0aW9uIFRvSW50ZXJ2YWwocGVyaW9kIEZISVIuUGVyaW9kKToNCiAgICBpZiBwZXJpb2QgaXMgbnVsbCB0aGVuDQogICAgICAgIG51bGwNCiAgICBlbHNlDQogICAgICAgIGlmIHBlcmlvZC4ic3RhcnQiIGlzIG51bGwgdGhlbg0KICAgICAgICAgICAgSW50ZXJ2YWwocGVyaW9kLiJzdGFydCIudmFsdWUsIHBlcmlvZC4iZW5kIi52YWx1ZV0NCiAgICAgICAgZWxzZQ0KICAgICAgICAgICAgSW50ZXJ2YWxbcGVyaW9kLiJzdGFydCIudmFsdWUsIHBlcmlvZC4iZW5kIi52YWx1ZV0NCg0KZGVmaW5lIGZ1bmN0aW9uIFRvQ2FsZW5kYXJVbml0KHVuaXQgU3lzdGVtLlN0cmluZyk6DQogICAgY2FzZSB1bml0DQogICAgICAgIHdoZW4gJ21zJyB0aGVuICdtaWxsaXNlY29uZCcNCiAgICAgICAgd2hlbiAncycgdGhlbiAnc2Vjb25kJw0KICAgICAgICB3aGVuICdtaW4nIHRoZW4gJ21pbnV0ZScNCiAgICAgICAgd2hlbiAnaCcgdGhlbiAnaG91cicNCiAgICAgICAgd2hlbiAnZCcgdGhlbiAnZGF5Jw0KICAgICAgICB3aGVuICd3aycgdGhlbiAnd2VlaycNCiAgICAgICAgd2hlbiAnbW8nIHRoZW4gJ21vbnRoJw0KICAgICAgICB3aGVuICdhJyB0aGVuICd5ZWFyJw0KICAgICAgICBlbHNlIHVuaXQNCiAgICBlbmQNCg0KZGVmaW5lIGZ1bmN0aW9uIFRvUXVhbnRpdHkocXVhbnRpdHkgRkhJUi5RdWFudGl0eSk6DQogICAgY2FzZQ0KICAgICAgICB3aGVuIHF1YW50aXR5IGlzIG51bGwgdGhlbiBudWxsDQogICAgICAgIHdoZW4gcXVhbnRpdHkudmFsdWUgaXMgbnVsbCB0aGVuIG51bGwNCiAgICAgICAgd2hlbiBxdWFudGl0eS5jb21wYXJhdG9yIGlzIG5vdCBudWxsIHRoZW4NCiAgICAgICAgICAgIE1lc3NhZ2UobnVsbCwgdHJ1ZSwgJ0ZISVJIZWxwZXJzLlRvUXVhbnRpdHkuQ29tcGFyYXRvclF1YW50aXR5Tm90U3VwcG9ydGVkJywgJ0Vycm9yJywgJ0ZISVIgUXVhbnRpdHkgdmFsdWUgaGFzIGEgY29tcGFyYXRvciBhbmQgY2Fubm90IGJlIGNvbnZlcnRlZCB0byBhIFN5c3RlbS5RdWFudGl0eSB2YWx1ZS4nKQ0KICAgICAgICB3aGVuIHF1YW50aXR5LnN5c3RlbSBpcyBudWxsIG9yIHF1YW50aXR5LnN5c3RlbS52YWx1ZSA9ICdodHRwOi8vdW5pdHNvZm1lYXN1cmUub3JnJw0KICAgICAgICAgICAgICBvciBxdWFudGl0eS5zeXN0ZW0udmFsdWUgPSAnaHR0cDovL2hsNy5vcmcvZmhpcnBhdGgvQ29kZVN5c3RlbS9jYWxlbmRhci11bml0cycgdGhlbg0KICAgICAgICAgICAgU3lzdGVtLlF1YW50aXR5IHsgdmFsdWU6IHF1YW50aXR5LnZhbHVlLnZhbHVlLCB1bml0OiBUb0NhbGVuZGFyVW5pdChDb2FsZXNjZShxdWFudGl0eS5jb2RlLnZhbHVlLCBxdWFudGl0eS51bml0LnZhbHVlLCAnMScpKSB9DQogICAgICAgIGVsc2UNCiAgICAgICAgICAgIE1lc3NhZ2UobnVsbCwgdHJ1ZSwgJ0ZISVJIZWxwZXJzLlRvUXVhbnRpdHkuSW52YWxpZEZISVJRdWFudGl0eScsICdFcnJvcicsICdJbnZhbGlkIEZISVIgUXVhbnRpdHkgY29kZTogJyAmIHF1YW50aXR5LnVuaXQudmFsdWUgJiAnICgnICYgcXVhbnRpdHkuc3lzdGVtLnZhbHVlICYgJ3wnICYgcXVhbnRpdHkuY29kZS52YWx1ZSAmICcpJykNCiAgICBlbmQNCg0KZGVmaW5lIGZ1bmN0aW9uIFRvUXVhbnRpdHlJZ25vcmluZ0NvbXBhcmF0b3IocXVhbnRpdHkgRkhJUi5RdWFudGl0eSk6DQogICAgY2FzZQ0KICAgICAgICB3aGVuIHF1YW50aXR5IGlzIG51bGwgdGhlbiBudWxsDQogICAgICAgIHdoZW4gcXVhbnRpdHkudmFsdWUgaXMgbnVsbCB0aGVuIG51bGwNCiAgICAgICAgd2hlbiBxdWFudGl0eS5zeXN0ZW0gaXMgbnVsbCBvciBxdWFudGl0eS5zeXN0ZW0udmFsdWUgPSAnaHR0cDovL3VuaXRzb2ZtZWFzdXJlLm9yZycNCiAgICAgICAgICAgICAgb3IgcXVhbnRpdHkuc3lzdGVtLnZhbHVlID0gJ2h0dHA6Ly9obDcub3JnL2ZoaXJwYXRoL0NvZGVTeXN0ZW0vY2FsZW5kYXItdW5pdHMnIHRoZW4NCiAgICAgICAgICAgIFN5c3RlbS5RdWFudGl0eSB7IHZhbHVlOiBxdWFudGl0eS52YWx1ZS52YWx1ZSwgdW5pdDogVG9DYWxlbmRhclVuaXQoQ29hbGVzY2UocXVhbnRpdHkuY29kZS52YWx1ZSwgcXVhbnRpdHkudW5pdC52YWx1ZSwgJzEnKSkgfQ0KICAgICAgICBlbHNlDQogICAgICAgICAgICBNZXNzYWdlKG51bGwsIHRydWUsICdGSElSSGVscGVycy5Ub1F1YW50aXR5LkludmFsaWRGSElSUXVhbnRpdHknLCAnRXJyb3InLCAnSW52YWxpZCBGSElSIFF1YW50aXR5IGNvZGU6ICcgJiBxdWFudGl0eS51bml0LnZhbHVlICYgJyAoJyAmIHF1YW50aXR5LnN5c3RlbS52YWx1ZSAmICd8JyAmIHF1YW50aXR5LmNvZGUudmFsdWUgJiAnKScpDQogICAgZW5kDQoNCmRlZmluZSBmdW5jdGlvbiBUb0ludGVydmFsKHF1YW50aXR5IEZISVIuUXVhbnRpdHkpOg0KICAgIGlmIHF1YW50aXR5IGlzIG51bGwgdGhlbiBudWxsIGVsc2UNCiAgICAgICAgY2FzZSBxdWFudGl0eS5jb21wYXJhdG9yLnZhbHVlDQogICAgICAgICAgICB3aGVuICc8JyB0aGVuDQogICAgICAgICAgICAgICAgSW50ZXJ2YWxbDQogICAgICAgICAgICAgICAgICAgIG51bGwsDQogICAgICAgICAgICAgICAgICAgIFRvUXVhbnRpdHlJZ25vcmluZ0NvbXBhcmF0b3IocXVhbnRpdHkpDQogICAgICAgICAgICAgICAgKQ0KICAgICAgICAgICAgd2hlbiAnPD0nIHRoZW4NCiAgICAgICAgICAgICAgICBJbnRlcnZhbFsNCiAgICAgICAgICAgICAgICAgICAgbnVsbCwNCiAgICAgICAgICAgICAgICAgICAgVG9RdWFudGl0eUlnbm9yaW5nQ29tcGFyYXRvcihxdWFudGl0eSkNCiAgICAgICAgICAgICAgICBdDQogICAgICAgICAgICB3aGVuICc+PScgdGhlbg0KICAgICAgICAgICAgICAgIEludGVydmFsWw0KICAgICAgICAgICAgICAgICAgICBUb1F1YW50aXR5SWdub3JpbmdDb21wYXJhdG9yKHF1YW50aXR5KSwNCiAgICAgICAgICAgICAgICAgICAgbnVsbA0KICAgICAgICAgICAgICAgIF0NCiAgICAgICAgICAgIHdoZW4gJz4nIHRoZW4NCiAgICAgICAgICAgICAgICBJbnRlcnZhbCgNCiAgICAgICAgICAgICAgICAgICAgVG9RdWFudGl0eUlnbm9yaW5nQ29tcGFyYXRvcihxdWFudGl0eSksDQogICAgICAgICAgICAgICAgICAgIG51bGwNCiAgICAgICAgICAgICAgICBdDQogICAgICAgICAgICBlbHNlDQogICAgICAgICAgICAgICAgSW50ZXJ2YWxbVG9RdWFudGl0eShxdWFudGl0eSksIFRvUXVhbnRpdHkocXVhbnRpdHkpXQ0KICAgICAgICBlbmQNCg0KZGVmaW5lIGZ1bmN0aW9uIFRvUmF0aW8ocmF0aW8gRkhJUi5SYXRpbyk6DQogICAgaWYgcmF0aW8gaXMgbnVsbCB0aGVuDQogICAgICAgIG51bGwNCiAgICBlbHNlDQogICAgICAgIFN5c3RlbS5SYXRpbyB7IG51bWVyYXRvcjogVG9RdWFudGl0eShyYXRpby5udW1lcmF0b3IpLCBkZW5vbWluYXRvcjogVG9RdWFudGl0eShyYXRpby5kZW5vbWluYXRvcikgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gVG9JbnRlcnZhbChyYW5nZSBGSElSLlJhbmdlKToNCiAgICBpZiByYW5nZSBpcyBudWxsIHRoZW4NCiAgICAgICAgbnVsbA0KICAgIGVsc2UNCiAgICAgICAgSW50ZXJ2YWxbVG9RdWFudGl0eShyYW5nZS5sb3cpLCBUb1F1YW50aXR5KHJhbmdlLmhpZ2gpXQ0KDQpkZWZpbmUgZnVuY3Rpb24gVG9Db2RlKGNvZGluZyBGSElSLkNvZGluZyk6DQogICAgaWYgY29kaW5nIGlzIG51bGwgdGhlbg0KICAgICAgICBudWxsDQogICAgZWxzZQ0KICAgICAgICBTeXN0ZW0uQ29kZSB7DQogICAgICAgICAgY29kZTogY29kaW5nLmNvZGUudmFsdWUsDQogICAgICAgICAgc3lzdGVtOiBjb2Rpbmcuc3lzdGVtLnZhbHVlLA0KICAgICAgICAgIHZlcnNpb246IGNvZGluZy52ZXJzaW9uLnZhbHVlLA0KICAgICAgICAgIGRpc3BsYXk6IGNvZGluZy5kaXNwbGF5LnZhbHVlDQogICAgICAgIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIFRvQ29uY2VwdChjb25jZXB0IEZISVIuQ29kZWFibGVDb25jZXB0KToNCiAgICBpZiBjb25jZXB0IGlzIG51bGwgdGhlbg0KICAgICAgICBudWxsDQogICAgZWxzZQ0KICAgICAgICBTeXN0ZW0uQ29uY2VwdCB7DQogICAgICAgICAgICBjb2RlczogY29uY2VwdC5jb2RpbmcgQyByZXR1cm4gVG9Db2RlKEMpLA0KICAgICAgICAgICAgZGlzcGxheTogY29uY2VwdC50ZXh0LnZhbHVlDQogICAgICAgIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIHJlZmVyZW5jZShyZWZlcmVuY2UgU3RyaW5nKToNCiAgICBpZiByZWZlcmVuY2UgaXMgbnVsbCB0aGVuDQogICAgICAgIG51bGwNCiAgICBlbHNlDQogICAgICAgIFJlZmVyZW5jZSB7IHJlZmVyZW5jZTogc3RyaW5nIHsgdmFsdWU6IHJlZmVyZW5jZSB9IH0NCg0KZGVmaW5lIGZ1bmN0aW9uIHJlc29sdmUocmVmZXJlbmNlIFN0cmluZykgcmV0dXJucyBSZXNvdXJjZTogZXh0ZXJuYWwNCmRlZmluZSBmdW5jdGlvbiByZXNvbHZlKHJlZmVyZW5jZSBSZWZlcmVuY2UpIHJldHVybnMgUmVzb3VyY2U6IGV4dGVybmFsDQpkZWZpbmUgZnVuY3Rpb24gcmVmZXJlbmNlKHJlc291cmNlIFJlc291cmNlKSByZXR1cm5zIFJlZmVyZW5jZTogZXh0ZXJuYWwNCmRlZmluZSBmdW5jdGlvbiBleHRlbnNpb24oZWxlbWVudCBFbGVtZW50LCB1cmwgU3RyaW5nKSByZXR1cm5zIExpc3Q8RWxlbWVudD46IGV4dGVybmFsDQpkZWZpbmUgZnVuY3Rpb24gZXh0ZW5zaW9uKHJlc291cmNlIFJlc291cmNlLCB1cmwgU3RyaW5nKSByZXR1cm5zIExpc3Q8RWxlbWVudD46IGV4dGVybmFsDQpkZWZpbmUgZnVuY3Rpb24gaGFzVmFsdWUoZWxlbWVudCBFbGVtZW50KSByZXR1cm5zIEJvb2xlYW46IGV4dGVybmFsDQpkZWZpbmUgZnVuY3Rpb24gZ2V0VmFsdWUoZWxlbWVudCBFbGVtZW50KSByZXR1cm5zIEFueTogZXh0ZXJuYWwNCmRlZmluZSBmdW5jdGlvbiBvZlR5cGUoaWRlbnRpZmllciBTdHJpbmcpIHJldHVybnMgTGlzdDxBbnk+OiBleHRlcm5hbA0KZGVmaW5lIGZ1bmN0aW9uIGlzKGlkZW50aWZpZXIgU3RyaW5nKSByZXR1cm5zIEJvb2xlYW46IGV4dGVybmFsDQpkZWZpbmUgZnVuY3Rpb24gYXMoaWRlbnRpZmllciBTdHJpbmcpIHJldHVybnMgQW55OiBleHRlcm5hbA0KZGVmaW5lIGZ1bmN0aW9uIGVsZW1lbnREZWZpbml0aW9uKGVsZW1lbnQgRWxlbWVudCkgcmV0dXJucyBFbGVtZW50RGVmaW5pdGlvbjogZXh0ZXJuYWwNCmRlZmluZSBmdW5jdGlvbiBzbGljZShlbGVtZW50IEVsZW1lbnQsIHVybCBTdHJpbmcsIG5hbWUgU3RyaW5nKSByZXR1cm5zIExpc3Q8RWxlbWVudD46IGV4dGVybmFsDQpkZWZpbmUgZnVuY3Rpb24gY2hlY2tNb2RpZmllcnMocmVzb3VyY2UgUmVzb3VyY2UpIHJldHVybnMgUmVzb3VyY2U6IGV4dGVybmFsDQpkZWZpbmUgZnVuY3Rpb24gY2hlY2tNb2RpZmllcnMocmVzb3VyY2UgUmVzb3VyY2UsIG1vZGlmaWVyIFN0cmluZykgcmV0dXJucyBSZXNvdXJjZTogZXh0ZXJuYWwNCmRlZmluZSBmdW5jdGlvbiBjaGVja01vZGlmaWVycyhlbGVtZW50IEVsZW1lbnQpIHJldHVybnMgRWxlbWVudDogZXh0ZXJuYWwNCmRlZmluZSBmdW5jdGlvbiBjaGVja01vZGlmaWVycyhlbGVtZW50IEVsZW1lbnQsIG1vZGlmaWVyIFN0cmluZykgcmV0dXJucyBFbGVtZW50OiBleHRlcm5hbA0KZGVmaW5lIGZ1bmN0aW9uIGNvbmZvcm1zVG8ocmVzb3VyY2UgUmVzb3VyY2UsIHN0cnVjdHVyZSBTdHJpbmcpIHJldHVybnMgQm9vbGVhbjogZXh0ZXJuYWwNCmRlZmluZSBmdW5jdGlvbiBtZW1iZXJPZihjb2RlIGNvZGUsIHZhbHVlU2V0IFN0cmluZykgcmV0dXJucyBCb29sZWFuOiBleHRlcm5hbA0KZGVmaW5lIGZ1bmN0aW9uIG1lbWJlck9mKGNvZGluZyBDb2RpbmcsIHZhbHVlU2V0IFN0cmluZykgcmV0dXJucyBCb29sZWFuOiBleHRlcm5hbA0KZGVmaW5lIGZ1bmN0aW9uIG1lbWJlck9mKGNvbmNlcHQgQ29kZWFibGVDb25jZXB0LCB2YWx1ZVNldCBTdHJpbmcpIHJldHVybnMgQm9vbGVhbjogZXh0ZXJuYWwNCmRlZmluZSBmdW5jdGlvbiBzdWJzdW1lcyhjb2RpbmcgQ29kaW5nLCBzdWJzdW1lZENvZGluZyBDb2RpbmcpIHJldHVybnMgQm9vbGVhbjogZXh0ZXJuYWwNCmRlZmluZSBmdW5jdGlvbiBzdWJzdW1lcyhjb25jZXB0IENvZGVhYmxlQ29uY2VwdCwgc3Vic3VtZWRDb25jZXB0IENvZGVhYmxlQ29uY2VwdCkgcmV0dXJucyBCb29sZWFuOiBleHRlcm5hbA0KZGVmaW5lIGZ1bmN0aW9uIHN1YnN1bWVkQnkoY29kaW5nIENvZGluZywgc3Vic3VtaW5nQ29kaW5nIENvZGluZykgcmV0dXJucyBCb29sZWFuOiBleHRlcm5hbA0KZGVmaW5lIGZ1bmN0aW9uIHN1YnN1bWVkQnkoY29uY2VwdCBDb2RlYWJsZUNvbmNlcHQsIHN1YnN1bWluZ0NvbmNlcHQgQ29kZWFibGVDb25jZXB0KSByZXR1cm5zIEJvb2xlYW46IGV4dGVybmFsDQpkZWZpbmUgZnVuY3Rpb24gaHRtbENoZWNrcyhlbGVtZW50IEVsZW1lbnQpIHJldHVybnMgQm9vbGVhbjogZXh0ZXJuYWwNCg0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEFjY291bnRTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEFjdGlvbkNhcmRpbmFsaXR5QmVoYXZpb3IpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEFjdGlvbkNvbmRpdGlvbktpbmQpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEFjdGlvbkdyb3VwaW5nQmVoYXZpb3IpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEFjdGlvblBhcnRpY2lwYW50VHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQWN0aW9uUHJlY2hlY2tCZWhhdmlvcik6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQWN0aW9uUmVsYXRpb25zaGlwVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQWN0aW9uUmVxdWlyZWRCZWhhdmlvcik6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQWN0aW9uU2VsZWN0aW9uQmVoYXZpb3IpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEFjdGl2aXR5RGVmaW5pdGlvbktpbmQpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEFjdGl2aXR5UGFydGljaXBhbnRUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBBZGRyZXNzVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQWRkcmVzc1VzZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQWRtaW5pc3RyYXRpdmVHZW5kZXIpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEFkdmVyc2VFdmVudEFjdHVhbGl0eSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQWdncmVnYXRpb25Nb2RlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBBbGxlcmd5SW50b2xlcmFuY2VDYXRlZ29yeSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQWxsZXJneUludG9sZXJhbmNlQ3JpdGljYWxpdHkpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEFsbGVyZ3lJbnRvbGVyYW5jZVNldmVyaXR5KTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBBbGxlcmd5SW50b2xlcmFuY2VUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBBcHBvaW50bWVudFN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQXNzZXJ0aW9uRGlyZWN0aW9uVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQXNzZXJ0aW9uT3BlcmF0b3JUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBBc3NlcnRpb25SZXNwb25zZVR5cGVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBBdWRpdEV2ZW50QWN0aW9uKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBBdWRpdEV2ZW50QWdlbnROZXR3b3JrVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQXVkaXRFdmVudE91dGNvbWUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEJpbmRpbmdTdHJlbmd0aCk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQmlvbG9naWNhbGx5RGVyaXZlZFByb2R1Y3RDYXRlZ29yeSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQmlvbG9naWNhbGx5RGVyaXZlZFByb2R1Y3RTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEJpb2xvZ2ljYWxseURlcml2ZWRQcm9kdWN0U3RvcmFnZVNjYWxlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBCdW5kbGVUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDYXBhYmlsaXR5U3RhdGVtZW50S2luZCk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQ2FyZVBsYW5BY3Rpdml0eUtpbmQpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENhcmVQbGFuQWN0aXZpdHlTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENhcmVQbGFuSW50ZW50KTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDYXJlUGxhblN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQ2FyZVRlYW1TdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENhdGFsb2dFbnRyeVJlbGF0aW9uVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQ2hhcmdlSXRlbURlZmluaXRpb25QcmljZUNvbXBvbmVudFR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENoYXJnZUl0ZW1TdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENsYWltUmVzcG9uc2VTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENsYWltU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDbGluaWNhbEltcHJlc3Npb25TdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENvZGVTZWFyY2hTdXBwb3J0KTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDb2RlU3lzdGVtQ29udGVudE1vZGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENvZGVTeXN0ZW1IaWVyYXJjaHlNZWFuaW5nKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDb21tdW5pY2F0aW9uUHJpb3JpdHkpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENvbW11bmljYXRpb25SZXF1ZXN0U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDb21tdW5pY2F0aW9uU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDb21wYXJ0bWVudENvZGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENvbXBhcnRtZW50VHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQ29tcG9zaXRpb25BdHRlc3RhdGlvbk1vZGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENvbXBvc2l0aW9uU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDb25jZXB0TWFwRXF1aXZhbGVuY2UpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENvbmNlcHRNYXBHcm91cFVubWFwcGVkTW9kZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQ29uZGl0aW9uYWxEZWxldGVTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENvbmRpdGlvbmFsUmVhZFN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQ29uc2VudERhdGFNZWFuaW5nKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDb25zZW50UHJvdmlzaW9uVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQ29uc2VudFN0YXRlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDb25zdHJhaW50U2V2ZXJpdHkpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIENvbnRhY3RQb2ludFN5c3RlbSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQ29udGFjdFBvaW50VXNlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDb250cmFjdFB1YmxpY2F0aW9uU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDb250cmFjdFN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQ29udHJpYnV0b3JUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBDb3ZlcmFnZVN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgQ3VycmVuY3lDb2RlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBEYXlPZldlZWspOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIERheXNPZldlZWspOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIERldGVjdGVkSXNzdWVTZXZlcml0eSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRGV0ZWN0ZWRJc3N1ZVN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRGV2aWNlTWV0cmljQ2FsaWJyYXRpb25TdGF0ZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRGV2aWNlTWV0cmljQ2FsaWJyYXRpb25UeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBEZXZpY2VNZXRyaWNDYXRlZ29yeSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRGV2aWNlTWV0cmljQ29sb3IpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIERldmljZU1ldHJpY09wZXJhdGlvbmFsU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBEZXZpY2VOYW1lVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRGV2aWNlUmVxdWVzdFN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRGV2aWNlVXNlU3RhdGVtZW50U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBEaWFnbm9zdGljUmVwb3J0U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBEaXNjcmltaW5hdG9yVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRG9jdW1lbnRDb25maWRlbnRpYWxpdHkpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIERvY3VtZW50TW9kZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRG9jdW1lbnRSZWZlcmVuY2VTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIERvY3VtZW50UmVsYXRpb25zaGlwVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRWxpZ2liaWxpdHlSZXF1ZXN0UHVycG9zZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRWxpZ2liaWxpdHlSZXF1ZXN0U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBFbGlnaWJpbGl0eVJlc3BvbnNlUHVycG9zZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRWxpZ2liaWxpdHlSZXNwb25zZVN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRW5hYmxlV2hlbkJlaGF2aW9yKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBFbmNvdW50ZXJMb2NhdGlvblN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRW5jb3VudGVyU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBFbmRwb2ludFN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRW5yb2xsbWVudFJlcXVlc3RTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEVucm9sbG1lbnRSZXNwb25zZVN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRXBpc29kZU9mQ2FyZVN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRXZlbnRDYXBhYmlsaXR5TW9kZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRXZlbnRUaW1pbmcpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEV2aWRlbmNlVmFyaWFibGVUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBFeGFtcGxlU2NlbmFyaW9BY3RvclR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEV4cGxhbmF0aW9uT2ZCZW5lZml0U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBFeHBvc3VyZVN0YXRlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBFeHRlbnNpb25Db250ZXh0VHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRkhJUkFsbFR5cGVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBGSElSRGVmaW5lZFR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEZISVJEZXZpY2VTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEZISVJSZXNvdXJjZVR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEZISVJTdWJzdGFuY2VTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEZISVJWZXJzaW9uKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBGYW1pbHlIaXN0b3J5U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBGaWx0ZXJPcGVyYXRvcik6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgRmxhZ1N0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgR29hbExpZmVjeWNsZVN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgR3JhcGhDb21wYXJ0bWVudFJ1bGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEdyYXBoQ29tcGFydG1lbnRVc2UpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEdyb3VwTWVhc3VyZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgR3JvdXBUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBHdWlkYW5jZVJlc3BvbnNlU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBHdWlkZVBhZ2VHZW5lcmF0aW9uKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBHdWlkZVBhcmFtZXRlckNvZGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEhUVFBWZXJiKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBJZGVudGlmaWVyVXNlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBJZGVudGl0eUFzc3VyYW5jZUxldmVsKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBJbWFnaW5nU3R1ZHlTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEltbXVuaXphdGlvbkV2YWx1YXRpb25TdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIEltbXVuaXphdGlvblN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgSW52b2ljZVByaWNlQ29tcG9uZW50VHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgSW52b2ljZVN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgSXNzdWVTZXZlcml0eSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgSXNzdWVUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBMaW5rVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgTGlua2FnZVR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIExpc3RNb2RlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBMaXN0U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBMb2NhdGlvbk1vZGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIExvY2F0aW9uU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBNZWFzdXJlUmVwb3J0U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBNZWFzdXJlUmVwb3J0VHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgTWVkaWFTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIE1lZGljYXRpb25BZG1pbmlzdHJhdGlvblN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgTWVkaWNhdGlvbkRpc3BlbnNlU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBNZWRpY2F0aW9uS25vd2xlZGdlU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBNZWRpY2F0aW9uUmVxdWVzdEludGVudCk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgTWVkaWNhdGlvblJlcXVlc3RQcmlvcml0eSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgTWVkaWNhdGlvblJlcXVlc3RTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIE1lZGljYXRpb25TdGF0ZW1lbnRTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIE1lZGljYXRpb25TdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIE1lc3NhZ2VTaWduaWZpY2FuY2VDYXRlZ29yeSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgTWVzc2FnZWhlYWRlcl9SZXNwb25zZV9SZXF1ZXN0KTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBNaW1lVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgTmFtZVVzZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgTmFtaW5nU3lzdGVtSWRlbnRpZmllclR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIE5hbWluZ1N5c3RlbVR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIE5hcnJhdGl2ZVN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgTm90ZVR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIE51dHJpdGlpb25PcmRlckludGVudCk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgTnV0cml0aW9uT3JkZXJTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIE9ic2VydmF0aW9uRGF0YVR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIE9ic2VydmF0aW9uUmFuZ2VDYXRlZ29yeSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgT2JzZXJ2YXRpb25TdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIE9wZXJhdGlvbktpbmQpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIE9wZXJhdGlvblBhcmFtZXRlclVzZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgT3JpZW50YXRpb25UeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBQYXJhbWV0ZXJVc2UpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFBhcnRpY2lwYW50UmVxdWlyZWQpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFBhcnRpY2lwYW50U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBQYXJ0aWNpcGF0aW9uU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBQYXltZW50Tm90aWNlU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBQYXltZW50UmVjb25jaWxpYXRpb25TdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFByb2NlZHVyZVN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgUHJvcGVydHlSZXByZXNlbnRhdGlvbik6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgUHJvcGVydHlUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBQcm92ZW5hbmNlRW50aXR5Um9sZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgUHVibGljYXRpb25TdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFF1YWxpdHlUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBRdWFudGl0eUNvbXBhcmF0b3IpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFF1ZXN0aW9ubmFpcmVJdGVtT3BlcmF0b3IpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFF1ZXN0aW9ubmFpcmVJdGVtVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgUXVlc3Rpb25uYWlyZVJlc3BvbnNlU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBSZWZlcmVuY2VIYW5kbGluZ1BvbGljeSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgUmVmZXJlbmNlVmVyc2lvblJ1bGVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBSZWZlcnJlZERvY3VtZW50U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBSZWxhdGVkQXJ0aWZhY3RUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBSZW1pdHRhbmNlT3V0Y29tZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgUmVwb3NpdG9yeVR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFJlcXVlc3RJbnRlbnQpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFJlcXVlc3RQcmlvcml0eSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgUmVxdWVzdFN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgUmVzZWFyY2hFbGVtZW50VHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgUmVzZWFyY2hTdHVkeVN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgUmVzZWFyY2hTdWJqZWN0U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBSZXNvdXJjZVR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFJlc291cmNlVmVyc2lvblBvbGljeSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgUmVzcG9uc2VUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBSZXN0ZnVsQ2FwYWJpbGl0eU1vZGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFJpc2tBc3Nlc3NtZW50U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBTUERYTGljZW5zZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgU2VhcmNoQ29tcGFyYXRvcik6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgU2VhcmNoRW50cnlNb2RlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBTZWFyY2hNb2RpZmllckNvZGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFNlYXJjaFBhcmFtVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgU2VjdGlvbk1vZGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFNlcXVlbmNlVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgU2VydmljZVJlcXVlc3RJbnRlbnQpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFNlcnZpY2VSZXF1ZXN0UHJpb3JpdHkpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFNlcnZpY2VSZXF1ZXN0U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBTbGljaW5nUnVsZXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFNsb3RTdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFNvcnREaXJlY3Rpb24pOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFNwZWNpbWVuQ29udGFpbmVkUHJlZmVyZW5jZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgU3BlY2ltZW5TdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgU3RyYW5kVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgU3RydWN0dXJlRGVmaW5pdGlvbktpbmQpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFN0cnVjdHVyZU1hcENvbnRleHRUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBTdHJ1Y3R1cmVNYXBHcm91cFR5cGVNb2RlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBTdHJ1Y3R1cmVNYXBJbnB1dE1vZGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFN0cnVjdHVyZU1hcE1vZGVsTW9kZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgU3RydWN0dXJlTWFwU291cmNlTGlzdE1vZGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFN0cnVjdHVyZU1hcFRhcmdldExpc3RNb2RlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBTdHJ1Y3R1cmVNYXBUcmFuc2Zvcm0pOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFN1YnNjcmlwdGlvbkNoYW5uZWxUeXBlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBTdWJzY3JpcHRpb25TdGF0dXMpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFN1cHBseURlbGl2ZXJ5U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBTdXBwbHlSZXF1ZXN0U3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBTeXN0ZW1SZXN0ZnVsSW50ZXJhY3Rpb24pOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFRhc2tJbnRlbnQpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFRhc2tQcmlvcml0eSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgVGFza1N0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgVGVzdFJlcG9ydEFjdGlvblJlc3VsdCk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgVGVzdFJlcG9ydFBhcnRpY2lwYW50VHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgVGVzdFJlcG9ydFJlc3VsdCk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgVGVzdFJlcG9ydFN0YXR1cyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgVGVzdFNjcmlwdFJlcXVlc3RNZXRob2RDb2RlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBUcmlnZ2VyVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgVHlwZURlcml2YXRpb25SdWxlKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBUeXBlUmVzdGZ1bEludGVyYWN0aW9uKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBVRElFbnRyeVR5cGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFVuaXRzT2ZUaW1lKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBVc2UpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIFZhcmlhYmxlVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgVmlzaW9uQmFzZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgVmlzaW9uRXllcyk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgVmlzaW9uU3RhdHVzKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBYUGF0aFVzYWdlVHlwZSk6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9TdHJpbmcodmFsdWUgYmFzZTY0QmluYXJ5KTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb0Jvb2xlYW4odmFsdWUgYm9vbGVhbik6IHZhbHVlLnZhbHVlDQpkZWZpbmUgZnVuY3Rpb24gVG9EYXRlKHZhbHVlIGRhdGUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvRGF0ZVRpbWUodmFsdWUgZGF0ZVRpbWUpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvRGVjaW1hbCh2YWx1ZSBkZWNpbWFsKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb0RhdGVUaW1lKHZhbHVlIGluc3RhbnQpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvSW50ZWdlcih2YWx1ZSBpbnRlZ2VyKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSBzdHJpbmcpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvVGltZSh2YWx1ZSB0aW1lKTogdmFsdWUudmFsdWUNCmRlZmluZSBmdW5jdGlvbiBUb1N0cmluZyh2YWx1ZSB1cmkpOiB2YWx1ZS52YWx1ZQ0KZGVmaW5lIGZ1bmN0aW9uIFRvU3RyaW5nKHZhbHVlIHhodG1sKTogdmFsdWUudmFsdWUNCg=="
			}
		  ]
		},
		"request": {
		  "method": "PUT",
		  "url": "Library/FHIRHelpers"
		}
	  },
	  {
		"resource": {
		  "resourceType": "Library",
		  "id": "NHSNdQMAcuteCareHospitalInitialPopulation",
		  "contained": [
			{
			  "resourceType": "Parameters",
			  "id": "options",
			  "parameter": [
				{
				  "name": "translatorVersion",
				  "valueString": "3.5.1"
				},
				{
				  "name": "option",
				  "valueString": "EnableDateRangeOptimization"
				},
				{
				  "name": "option",
				  "valueString": "EnableAnnotations"
				},
				{
				  "name": "option",
				  "valueString": "EnableLocators"
				},
				{
				  "name": "option",
				  "valueString": "DisableListDemotion"
				},
				{
				  "name": "option",
				  "valueString": "DisableListPromotion"
				},
				{
				  "name": "analyzeDataRequirements",
				  "valueBoolean": false
				},
				{
				  "name": "collapseDataRequirements",
				  "valueBoolean": true
				},
				{
				  "name": "compatibilityLevel",
				  "valueString": "1.5"
				},
				{
				  "name": "enableCqlOnly",
				  "valueBoolean": false
				},
				{
				  "name": "errorLevel",
				  "valueString": "Info"
				},
				{
				  "name": "signatureLevel",
				  "valueString": "None"
				},
				{
				  "name": "validateUnits",
				  "valueBoolean": true
				},
				{
				  "name": "verifyOnly",
				  "valueBoolean": false
				}
			  ]
			}
		  ],
		  "extension": [
			{
			  "url": "http://hl7.org/fhir/StructureDefinition/cqf-cqlOptions",
			  "valueReference": {
				"reference": "#options"
			  }
			}
		  ],
		  "url": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/NHSNdQMAcuteCareHospitalInitialPopulation",
		  "version": "0.0.014",
		  "name": "NHSNdQMAcuteCareHospitalInitialPopulation",
		  "type": {
			"coding": [
			  {
				"system": "http://terminology.hl7.org/CodeSystem/library-type",
				"code": "logic-library"
			  }
			]
		  },
		  "relatedArtifact": [
			{
			  "type": "depends-on",
			  "display": "FHIR model information",
			  "resource": "http://fhir.org/guides/cqf/common/Library/FHIR-ModelInfo|4.0.1"
			},
			{
			  "type": "depends-on",
			  "display": "Library FHIRHelpers",
			  "resource": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/FHIRHelpers|4.0.1"
			},
			{
			  "type": "depends-on",
			  "display": "Library Global",
			  "resource": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/MATGlobalCommonFunctionsFHIR4|6.1.000"
			},
			{
			  "type": "depends-on",
			  "display": "Library SDE",
			  "resource": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/SupplementalDataElementsFHIR4|2.0.000"
			},
			{
			  "type": "depends-on",
			  "display": "Library SharedResource",
			  "resource": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Library/SharedResourceCreation|0.1.005"
			},
			{
			  "type": "depends-on",
			  "display": "Code system ActCode",
			  "resource": "http://terminology.hl7.org/CodeSystem/v3-ActCode"
			},
			{
			  "type": "depends-on",
			  "display": "Code system Observation Category",
			  "resource": "http://terminology.hl7.org/CodeSystem/observation-category"
			},
			{
			  "type": "depends-on",
			  "display": "Code system LOINC",
			  "resource": "http://loinc.org"
			},
			{
			  "type": "depends-on",
			  "display": "Code system V2-0074",
			  "resource": "http://terminology.hl7.org/CodeSystem/v2-0074"
			},
			{
			  "type": "depends-on",
			  "display": "Code system Condition Category",
			  "resource": "http://terminology.hl7.org/CodeSystem/condition-category"
			},
			{
			  "type": "depends-on",
			  "display": "Value set Inpatient, Emergency, and Observation Locations",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1046.265"
			},
			{
			  "type": "depends-on",
			  "display": "Value set Emergency Department Visit",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.117.1.7.1.292"
			},
			{
			  "type": "depends-on",
			  "display": "Value set Encounter Inpatient",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307"
			},
			{
			  "type": "depends-on",
			  "display": "Value set Observation Services",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1111.143"
			},
			{
			  "type": "depends-on",
			  "display": "Value set NHSN Inpatient Encounter Class Codes",
			  "resource": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1046.274"
			}
		  ],
		  "parameter": [
			{
			  "name": "Measurement Period",
			  "use": "in",
			  "min": 0,
			  "max": "1",
			  "type": "Period"
			},
			{
			  "name": "Patient",
			  "use": "out",
			  "min": 0,
			  "max": "1",
			  "type": "Patient"
			},
			{
			  "name": "Qualifying Encounters During Measurement Period",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Encounter"
			},
			{
			  "name": "Encounters",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Encounter"
			},
			{
			  "name": "Encounters with Patient Hospital Locations",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Encounter"
			},
			{
			  "name": "Initial Population",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Encounter"
			},
			{
			  "name": "Conditions",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Condition"
			},
			{
			  "name": "DiagnosticReports",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "DiagnosticReport"
			},
			{
			  "name": "Observations",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Observation"
			},
			{
			  "name": "Get Locations from IP Encounters in Measurement Period",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Location"
			},
			{
			  "name": "SDE Condition",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Condition"
			},
			{
			  "name": "SDE Device",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Device"
			},
			{
			  "name": "SDE DiagnosticReport Lab",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "DiagnosticReport"
			},
			{
			  "name": "SDE DiagnosticReport Note",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "DiagnosticReport"
			},
			{
			  "name": "SDE DiagnosticReport Others",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "DiagnosticReport"
			},
			{
			  "name": "SDE Encounter",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Encounter"
			},
			{
			  "name": "SDE Location",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Location"
			},
			{
			  "name": "SDE Medication Administration",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "MedicationAdministration"
			},
			{
			  "name": "SDE Medication Request",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "MedicationRequest"
			},
			{
			  "name": "SDE Medication",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Medication"
			},
			{
			  "name": "SDE Observation Lab Category",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Observation"
			},
			{
			  "name": "SDE Observation Vital Signs Category",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Observation"
			},
			{
			  "name": "SDE Observation Category",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Observation"
			},
			{
			  "name": "SDE Coverage",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Coverage"
			},
			{
			  "name": "SDE Procedure",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Procedure"
			},
			{
			  "name": "SDE Specimen",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "Specimen"
			},
			{
			  "name": "SDE Service Request",
			  "use": "out",
			  "min": 0,
			  "max": "*",
			  "type": "ServiceRequest"
			},
			{
			  "name": "SDE Minimal Patient",
			  "use": "out",
			  "min": 0,
			  "max": "1",
			  "type": "Patient"
			}
		  ],
		  "dataRequirement": [
			{
			  "type": "Patient",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Patient"
			  ],
			  "mustSupport": [
				"id",
				"identifier",
				"active",
				"name",
				"telecom",
				"gender",
				"birthDate",
				"deceased",
				"address",
				"maritalStatus",
				"multipleBirth",
				"photo",
				"contact",
				"communication",
				"generalPractitioner",
				"managingOrganization",
				"link"
			  ]
			},
			{
			  "type": "Encounter",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Encounter"
			  ],
			  "mustSupport": [
				"type",
				"status",
				"period",
				"location",
				"diagnosis",
				"id",
				"extension",
				"identifier",
				"statusHistory",
				"class",
				"classHistory",
				"serviceType",
				"priority",
				"subject",
				"length",
				"reasonCode",
				"reasonReference",
				"account",
				"hospitalization",
				"partOf"
			  ],
			  "codeFilter": [
				{
				  "path": "type",
				  "valueSet": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307"
				}
			  ]
			},
			{
			  "type": "Encounter",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Encounter"
			  ],
			  "mustSupport": [
				"type",
				"status",
				"period",
				"location",
				"diagnosis",
				"id",
				"extension",
				"identifier",
				"statusHistory",
				"class",
				"classHistory",
				"serviceType",
				"priority",
				"subject",
				"length",
				"reasonCode",
				"reasonReference",
				"account",
				"hospitalization",
				"partOf"
			  ],
			  "codeFilter": [
				{
				  "path": "type",
				  "valueSet": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.117.1.7.1.292"
				}
			  ]
			},
			{
			  "type": "Encounter",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Encounter"
			  ],
			  "mustSupport": [
				"type",
				"status",
				"period",
				"location",
				"diagnosis",
				"id",
				"extension",
				"identifier",
				"statusHistory",
				"class",
				"classHistory",
				"serviceType",
				"priority",
				"subject",
				"length",
				"reasonCode",
				"reasonReference",
				"account",
				"hospitalization",
				"partOf"
			  ],
			  "codeFilter": [
				{
				  "path": "type",
				  "valueSet": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1111.143"
				}
			  ]
			},
			{
			  "type": "Encounter",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Encounter"
			  ],
			  "mustSupport": [
				"class",
				"status",
				"period",
				"location",
				"diagnosis",
				"id",
				"extension",
				"identifier",
				"statusHistory",
				"classHistory",
				"type",
				"serviceType",
				"priority",
				"subject",
				"length",
				"reasonCode",
				"reasonReference",
				"account",
				"hospitalization",
				"partOf"
			  ],
			  "codeFilter": [
				{
				  "path": "class",
				  "valueSet": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1046.274"
				}
			  ]
			},
			{
			  "type": "Encounter",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Encounter"
			  ],
			  "mustSupport": [
				"class",
				"status",
				"period",
				"location",
				"diagnosis",
				"id",
				"extension",
				"identifier",
				"statusHistory",
				"classHistory",
				"type",
				"serviceType",
				"priority",
				"subject",
				"length",
				"reasonCode",
				"reasonReference",
				"account",
				"hospitalization",
				"partOf"
			  ],
			  "codeFilter": [
				{
				  "path": "class",
				  "code": [
					{
					  "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
					  "code": "EMER",
					  "display": "emergency"
					}
				  ]
				}
			  ]
			},
			{
			  "type": "Encounter",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Encounter"
			  ],
			  "mustSupport": [
				"status",
				"period",
				"location",
				"diagnosis",
				"id",
				"extension",
				"identifier",
				"statusHistory",
				"class",
				"classHistory",
				"type",
				"serviceType",
				"priority",
				"subject",
				"length",
				"reasonCode",
				"reasonReference",
				"account",
				"hospitalization",
				"partOf"
			  ]
			},
			{
			  "type": "Location",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Location"
			  ],
			  "mustSupport": [
				"id",
				"extension",
				"status",
				"operationalStatus",
				"name",
				"alias",
				"description",
				"mode",
				"type",
				"telecom",
				"address",
				"physicalType",
				"position",
				"managingOrganization",
				"partOf",
				"hoursOfOperation",
				"availabilityExceptions",
				"endpoint"
			  ]
			},
			{
			  "type": "Condition",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Condition"
			  ],
			  "mustSupport": [
				"id",
				"extension",
				"clinicalStatus",
				"verificationStatus",
				"category",
				"severity",
				"code",
				"bodySite",
				"subject",
				"encounter",
				"onset",
				"abatement",
				"recordedDate",
				"stage",
				"evidence",
				"note",
				"encounter.id"
			  ]
			},
			{
			  "type": "DiagnosticReport",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/DiagnosticReport"
			  ],
			  "mustSupport": [
				"id",
				"extension",
				"basedOn",
				"status",
				"category",
				"code",
				"subject",
				"encounter",
				"effective",
				"issued",
				"performer",
				"resultsInterpreter",
				"specimen",
				"result",
				"conclusion",
				"conclusionCode"
			  ]
			},
			{
			  "type": "Observation",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Observation"
			  ],
			  "mustSupport": [
				"id",
				"extension",
				"basedOn",
				"partOf",
				"status",
				"category",
				"code",
				"subject",
				"focus",
				"encounter",
				"effective",
				"issued",
				"performer",
				"value",
				"dataAbsentReason",
				"interpretation",
				"note",
				"bodySite",
				"method",
				"specimen",
				"device",
				"referenceRange",
				"hasMember",
				"derivedFrom",
				"component"
			  ]
			},
			{
			  "type": "Device",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Device"
			  ],
			  "mustSupport": [
				"id",
				"extension",
				"definition",
				"udiCarrier",
				"status",
				"statusReason",
				"distinctIdentifier",
				"manufacturer",
				"manufactureDate",
				"expirationDate",
				"lotNumber",
				"serialNumber",
				"deviceName",
				"modelNumber",
				"partNumber",
				"type",
				"specialization",
				"version",
				"property",
				"patient",
				"owner",
				"contact",
				"location",
				"url",
				"note",
				"safety",
				"parent"
			  ]
			},
			{
			  "type": "MedicationAdministration",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/MedicationAdministration"
			  ],
			  "mustSupport": [
				"effective",
				"id",
				"extension",
				"instantiates",
				"partOf",
				"status",
				"statusReason",
				"category",
				"medication",
				"subject",
				"context",
				"supportingInformation",
				"performer",
				"reasonCode",
				"reasonReference",
				"request",
				"device",
				"note",
				"dosage",
				"eventHistory"
			  ]
			},
			{
			  "type": "MedicationRequest",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/MedicationRequest"
			  ],
			  "mustSupport": [
				"authoredOn",
				"id",
				"extension",
				"status",
				"statusReason",
				"intent",
				"category",
				"priority",
				"doNotPerform",
				"reported",
				"medication",
				"subject",
				"encounter",
				"requester",
				"recorder",
				"reasonCode",
				"reasonReference",
				"instantiatesCanonical",
				"instantiatesUri",
				"courseOfTherapyType",
				"dosageInstruction"
			  ]
			},
			{
			  "type": "Medication",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Medication"
			  ],
			  "mustSupport": [
				"id",
				"extension",
				"code",
				"status",
				"manufacturer",
				"form",
				"amount",
				"ingredient",
				"batch"
			  ]
			},
			{
			  "type": "Coverage",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Coverage"
			  ],
			  "mustSupport": [
				"period",
				"id",
				"extension",
				"status",
				"type",
				"policyHolder",
				"subscriber",
				"subscriberId",
				"beneficiary",
				"dependent",
				"relationship",
				"payor",
				"class",
				"order",
				"network",
				"subrogation",
				"contract"
			  ]
			},
			{
			  "type": "Procedure",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Procedure"
			  ],
			  "mustSupport": [
				"performed",
				"id",
				"extension",
				"instantiatesCanonical",
				"instantiatesUri",
				"basedOn",
				"partOf",
				"status",
				"statusReason",
				"category",
				"code",
				"subject",
				"encounter",
				"recorder",
				"asserter",
				"performer",
				"location",
				"reasonCode",
				"reasonReference",
				"bodySite",
				"outcome",
				"report",
				"complication",
				"complicationDetail",
				"followUp",
				"note",
				"focalDevice",
				"usedReference",
				"usedCode"
			  ]
			},
			{
			  "type": "Specimen",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/Specimen"
			  ],
			  "mustSupport": [
				"collection",
				"collection.collected",
				"id",
				"extension",
				"identifier",
				"accessionIdentifier",
				"status",
				"type",
				"subject",
				"receivedTime",
				"parent",
				"request",
				"processing",
				"container",
				"condition",
				"note"
			  ]
			},
			{
			  "type": "ServiceRequest",
			  "profile": [
				"http://hl7.org/fhir/StructureDefinition/ServiceRequest"
			  ],
			  "mustSupport": [
				"authoredOn",
				"id",
				"extension",
				"instantiatesCanonical",
				"instantiatesUri",
				"basedOn",
				"replaces",
				"requisition",
				"status",
				"intent",
				"category",
				"priority",
				"doNotPerform",
				"code",
				"orderDetail",
				"quantity",
				"subject",
				"encounter",
				"occurrence",
				"asNeeded",
				"requester",
				"performerType",
				"performer",
				"locationCode",
				"locationReference",
				"reasonCode",
				"reasonReference",
				"insurance",
				"supportingInfo",
				"specimen",
				"bodySite",
				"note",
				"patientInstruction",
				"relevantHistory"
			  ]
			}
		  ],
		  "content": [
			{
			  "contentType": "text/cql",
			  "data": "bGlicmFyeSBOSFNOZFFNQWN1dGVDYXJlSG9zcGl0YWxJbml0aWFsUG9wdWxhdGlvbiB2ZXJzaW9uICcwLjAuMDE0Jw0KDQp1c2luZyBGSElSIHZlcnNpb24gJzQuMC4xJw0KDQppbmNsdWRlIEZISVJIZWxwZXJzIHZlcnNpb24gJzQuMC4xJyBjYWxsZWQgRkhJUkhlbHBlcnMNCmluY2x1ZGUgTUFUR2xvYmFsQ29tbW9uRnVuY3Rpb25zRkhJUjQgdmVyc2lvbiAnNi4xLjAwMCcgY2FsbGVkIEdsb2JhbA0KaW5jbHVkZSBTdXBwbGVtZW50YWxEYXRhRWxlbWVudHNGSElSNCB2ZXJzaW9uICcyLjAuMDAwJyBjYWxsZWQgU0RFDQppbmNsdWRlIFNoYXJlZFJlc291cmNlQ3JlYXRpb24gdmVyc2lvbiAnMC4xLjAwNScgY2FsbGVkIFNoYXJlZFJlc291cmNlDQoNCmNvZGVzeXN0ZW0gIkFjdENvZGUiOiAnaHR0cDovL3Rlcm1pbm9sb2d5LmhsNy5vcmcvQ29kZVN5c3RlbS92My1BY3RDb2RlJw0KY29kZXN5c3RlbSAiT2JzZXJ2YXRpb24gQ2F0ZWdvcnkiOiAnaHR0cDovL3Rlcm1pbm9sb2d5LmhsNy5vcmcvQ29kZVN5c3RlbS9vYnNlcnZhdGlvbi1jYXRlZ29yeScNCmNvZGVzeXN0ZW0gIkxPSU5DIjogJ2h0dHA6Ly9sb2luYy5vcmcnIA0KY29kZXN5c3RlbSAiVjItMDA3NCI6ICdodHRwOi8vdGVybWlub2xvZ3kuaGw3Lm9yZy9Db2RlU3lzdGVtL3YyLTAwNzQnDQpjb2Rlc3lzdGVtICJDb25kaXRpb24gQ2F0ZWdvcnkiOiAnaHR0cDovL3Rlcm1pbm9sb2d5LmhsNy5vcmcvQ29kZVN5c3RlbS9jb25kaXRpb24tY2F0ZWdvcnknDQoNCnZhbHVlc2V0ICJJbnBhdGllbnQsIEVtZXJnZW5jeSwgYW5kIE9ic2VydmF0aW9uIExvY2F0aW9ucyI6ICdodHRwOi8vY3RzLm5sbS5uaWguZ292L2ZoaXIvVmFsdWVTZXQvMi4xNi44NDAuMS4xMTM3NjIuMS40LjEwNDYuMjY1Jw0KdmFsdWVzZXQgIkVtZXJnZW5jeSBEZXBhcnRtZW50IFZpc2l0IjogJ2h0dHA6Ly9jdHMubmxtLm5paC5nb3YvZmhpci9WYWx1ZVNldC8yLjE2Ljg0MC4xLjExMzg4My4zLjExNy4xLjcuMS4yOTInDQp2YWx1ZXNldCAiRW5jb3VudGVyIElucGF0aWVudCI6ICdodHRwOi8vY3RzLm5sbS5uaWguZ292L2ZoaXIvVmFsdWVTZXQvMi4xNi44NDAuMS4xMTM4ODMuMy42NjYuNS4zMDcnDQp2YWx1ZXNldCAiT2JzZXJ2YXRpb24gU2VydmljZXMiOiAnaHR0cDovL2N0cy5ubG0ubmloLmdvdi9maGlyL1ZhbHVlU2V0LzIuMTYuODQwLjEuMTEzNzYyLjEuNC4xMTExLjE0MycNCnZhbHVlc2V0ICJOSFNOIElucGF0aWVudCBFbmNvdW50ZXIgQ2xhc3MgQ29kZXMiOiAnaHR0cDovL2N0cy5ubG0ubmloLmdvdi9maGlyL1ZhbHVlU2V0LzIuMTYuODQwLjEuMTEzNzYyLjEuNC4xMDQ2LjI3NCcNCg0KLy9jb2RlIGZvciBPYnNlcnZhdGlvbiBDYXRlZ29yeQ0KY29kZSAibGFib3JhdG9yeSI6ICdsYWJvcmF0b3J5JyBmcm9tICJPYnNlcnZhdGlvbiBDYXRlZ29yeSIgZGlzcGxheSAnTGFib3JhdG9yeScNCmNvZGUgInNvY2lhbC1oaXN0b3J5IjogJ3NvY2lhbC1oaXN0b3J5JyBmcm9tICJPYnNlcnZhdGlvbiBDYXRlZ29yeSIgZGlzcGxheSAnU29jaWFsIEhpc3RvcnknDQpjb2RlICJ2aXRhbC1zaWducyI6ICd2aXRhbC1zaWducycgZnJvbSAiT2JzZXJ2YXRpb24gQ2F0ZWdvcnkiIGRpc3BsYXkgJ1ZpdGFsIFNpZ25zJw0KY29kZSAiaW1hZ2luZyI6ICdpbWFnaW5nJyBmcm9tICJPYnNlcnZhdGlvbiBDYXRlZ29yeSIgZGlzcGxheSAnSW1hZ2luZycNCmNvZGUgInByb2NlZHVyZSI6ICdwcm9jZWR1cmUnIGZyb20gIk9ic2VydmF0aW9uIENhdGVnb3J5IiBkaXNwbGF5ICdQcm9jZWR1cmUnDQpjb2RlICJzdXJ2ZXkiOiAnc3VydmV5JyBmcm9tICJPYnNlcnZhdGlvbiBDYXRlZ29yeSIgZGlzcGxheSAnU3VydmV5Jw0KDQpjb2RlICJwcm9ibGVtLWxpc3QtaXRlbSI6ICdwcm9ibGVtLWxpc3QtaXRlbScgZnJvbSAiQ29uZGl0aW9uIENhdGVnb3J5IiBkaXNwbGF5ICdQcm9ibGVtIExpc3QgSXRlbScNCmNvZGUgImVuY291bnRlci1kaWFnbm9zaXMiOiAnZW5jb3VudGVyLWRpYWdub3NpcycgZnJvbSAiQ29uZGl0aW9uIENhdGVnb3J5IiBkaXNwbGF5ICdFbmNvdW50ZXIgRGlhZ25vc2lzJw0KDQovL2NvZGUgZm9yIERpYWdub3N0aWMgUmVwb3J0IENhdGVnb3J5DQpjb2RlICJMQUIiOiAnTEFCJyBmcm9tICJWMi0wMDc0IiBkaXNwbGF5ICdMYWJvcmF0b3J5Jw0KY29kZSAiUmFkaW9sb2d5IjogJ0xQMjk2ODQtNScgZnJvbSAiTE9JTkMiIGRpc3BsYXkgJ1JhZGlvbG9neScNCmNvZGUgIlBhdGhvbG9neSI6ICdMUDc4MzktNicgZnJvbSAiTE9JTkMiIGRpc3BsYXkgJ1BhdGhvbG9neScNCmNvZGUgIkNhcmRpb2xvZ3kiOiAnTFAyOTcwOC0yJyBmcm9tICJMT0lOQyIgZGlzcGxheSAnQ2FyZGlvbG9neScNCg0KLy9jb2RlIGZvciBFbWVyZ2VuY3kgRW5jb3VudGVyIENsYXNzDQpjb2RlICJlbWVyZ2VuY3kiOiAnRU1FUicgZnJvbSAiQWN0Q29kZSIgZGlzcGxheSAnZW1lcmdlbmN5Jw0KDQpwYXJhbWV0ZXIgIk1lYXN1cmVtZW50IFBlcmlvZCIgDQogICAgZGVmYXVsdCBJbnRlcnZhbFtAMjAyNC0wMy0wMVQwMDowMDowMC4wLCBAMjAyNC0wMy0zMVQwMDowMDowMC4wKQ0KDQpjb250ZXh0IFBhdGllbnQgDQoNCmRlZmluZSAiUXVhbGlmeWluZyBFbmNvdW50ZXJzIER1cmluZyBNZWFzdXJlbWVudCBQZXJpb2QiOg0KICggW0VuY291bnRlcjogIkVuY291bnRlciBJbnBhdGllbnQiXQ0KICB1bmlvbiBbRW5jb3VudGVyOiAiRW1lcmdlbmN5IERlcGFydG1lbnQgVmlzaXQiXQ0KICB1bmlvbiBbRW5jb3VudGVyOiAiT2JzZXJ2YXRpb24gU2VydmljZXMiXQ0KICB1bmlvbiBbRW5jb3VudGVyOiBjbGFzcyBpbiAiTkhTTiBJbnBhdGllbnQgRW5jb3VudGVyIENsYXNzIENvZGVzIl0NCiAgdW5pb24gW0VuY291bnRlcjogY2xhc3MgfiAiZW1lcmdlbmN5Il0pIFF1YWxpZnlpbmdFbmNvdW50ZXJzDQogIHdoZXJlIFF1YWxpZnlpbmdFbmNvdW50ZXJzLnN0YXR1cyBpbiB7J2luLXByb2dyZXNzJywgJ2ZpbmlzaGVkJywgJ3RyaWFnZWQnLCAnb25sZWF2ZScsICdlbnRlcmVkLWluLWVycm9yJ30NCiAgICBhbmQgUXVhbGlmeWluZ0VuY291bnRlcnMucGVyaW9kIG92ZXJsYXBzICJNZWFzdXJlbWVudCBQZXJpb2QiIA0KDQpkZWZpbmUgIkVuY291bnRlcnMgd2l0aCBQYXRpZW50IEhvc3BpdGFsIExvY2F0aW9ucyI6DQogICJFbmNvdW50ZXJzIiBFbmNvdW50ZXJzDQogIHdoZXJlIGV4aXN0cygNCiAgICBFbmNvdW50ZXJzLmxvY2F0aW9uIEVuY291bnRlckxvY2F0aW9uDQogICAgd2hlcmUgR2xvYmFsLkdldExvY2F0aW9uKEVuY291bnRlckxvY2F0aW9uLmxvY2F0aW9uKS50eXBlIGluICJJbnBhdGllbnQsIEVtZXJnZW5jeSwgYW5kIE9ic2VydmF0aW9uIExvY2F0aW9ucyINCiAgICAgIGFuZCBFbmNvdW50ZXJMb2NhdGlvbi5wZXJpb2Qgb3ZlcmxhcHMgRW5jb3VudGVycy5wZXJpb2QNCiAgKQ0KICBhbmQgRW5jb3VudGVycy5zdGF0dXMgaW4geydpbi1wcm9ncmVzcycsICdmaW5pc2hlZCcsICd0cmlhZ2VkJywgJ29ubGVhdmUnLCAnZW50ZXJlZC1pbi1lcnJvcid9DQogIGFuZCBFbmNvdW50ZXJzLnBlcmlvZCBvdmVybGFwcyAiTWVhc3VyZW1lbnQgUGVyaW9kIg0KDQpkZWZpbmUgIkluaXRpYWwgUG9wdWxhdGlvbiI6DQogICJRdWFsaWZ5aW5nIEVuY291bnRlcnMgRHVyaW5nIE1lYXN1cmVtZW50IFBlcmlvZCINCiAgdW5pb24gIkVuY291bnRlcnMgd2l0aCBQYXRpZW50IEhvc3BpdGFsIExvY2F0aW9ucyINCg0KZGVmaW5lICJFbmNvdW50ZXJzIjoNCiAgW0VuY291bnRlcl0NCg0KZGVmaW5lICJDb25kaXRpb25zIjoNCiAgW0NvbmRpdGlvbl0NCg0KZGVmaW5lICJEaWFnbm9zdGljUmVwb3J0cyI6DQogIFtEaWFnbm9zdGljUmVwb3J0XQ0KDQpkZWZpbmUgIk9ic2VydmF0aW9ucyI6DQogIFtPYnNlcnZhdGlvbl0NCg0KZGVmaW5lICJHZXQgTG9jYXRpb25zIGZyb20gSVAgRW5jb3VudGVycyBpbiBNZWFzdXJlbWVudCBQZXJpb2QiOg0KICBmbGF0dGVuKCJJbml0aWFsIFBvcHVsYXRpb24iIElQDQogIGxldCBsb2NhdGlvbkVsZW1lbnRzOiBJUC5sb2NhdGlvbg0KICByZXR1cm4NCiAgICBsb2NhdGlvbkVsZW1lbnRzIExFDQogICAgbGV0IGxvY2F0aW9uUmVmZXJlbmNlOiBMRS5sb2NhdGlvbg0KICAgIHJldHVybiBHbG9iYWwuR2V0TG9jYXRpb24obG9jYXRpb25SZWZlcmVuY2UpKQ0KDQovLz09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0NCi8vU3VwcGxlbWVudGFsIERhdGEgRWxlbWVudA0KLy9XaGVuIEZISVIuY2Fub25pY2FsIHZhbHVlIGlzIHByZXNlbnQsIFVTIENvcmUgMy4xLjEgcHJvZmlsZXMgYXJlIHVzZWQNCi8vV2hlbiBGSElSLmNhbm9uaWNhbCB2YWx1ZSBpcyBub3QgcHJlc2VudCwgRkhJUiBCYXNlIHByb2ZpbGVzIGFyZSB1c2VkDQovLz09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0NCmRlZmluZSAiU0RFIENvbmRpdGlvbiI6DQogICJDb25kaXRpb25zIiBDb25kaXRpb25zIA0KICB3aGVyZSBleGlzdHMoDQogICAgIkluaXRpYWwgUG9wdWxhdGlvbiIgSVANCiAgICAvL0NoZWNrIGZvciBQcm9ibGVtIExpc3QgQ29uZGl0aW9ucyB0aGF0IHdlcmUgcmVjb3JkZWQgYmVmb3JlIG9yIGR1cmluZyBJUA0KICAgIHdoZXJlICgNCiAgICAgIChDb25kaXRpb25zLnJlY29yZGVkRGF0ZSBiZWZvcmUgc3RhcnQgb2YgSVAucGVyaW9kDQogICAgICAgIG9yIENvbmRpdGlvbnMucmVjb3JkZWREYXRlIGR1cmluZyBJUC5wZXJpb2QpDQogICAgICBhbmQgZXhpc3RzKENvbmRpdGlvbnMuY2F0ZWdvcnkgY2F0ZWdvcmllcw0KICAgICAgICB3aGVyZSBjYXRlZ29yaWVzIH4gInByb2JsZW0tbGlzdC1pdGVtIikpDQogICAgLy9DaGVjayBmb3IgRW5jb3VudGVyIERpYWdub3NpcyBDb25kaXRpb25zIHRoYXQgcmVmZXJlbmNlIGFuIElQIGVuY291bnRlcg0KICAgIG9yICgNCiAgICAgIChleGlzdHMoSVAuZGlhZ25vc2lzIERpYWdub3Nlcw0KICAgICAgICAgIHdoZXJlIEdldENvbmRpdGlvbihEaWFnbm9zZXMuY29uZGl0aW9uKS5pZCA9IENvbmRpdGlvbnMuaWQNCiAgICAgICAgKQ0KICAgICAgICBvciBHZXRFbmNvdW50ZXIoQ29uZGl0aW9ucy5lbmNvdW50ZXIpLmlkID0gSVAuaWQNCiAgICAgICkNCiAgICAgIGFuZCBleGlzdHMgKENvbmRpdGlvbnMuY2F0ZWdvcnkgY2F0ZWdvcmllcw0KICAgICAgICB3aGVyZSBjYXRlZ29yaWVzIH4gImVuY291bnRlci1kaWFnbm9zaXMiKQ0KICAgICkNCiAgKQ0KICByZXR1cm4gU2hhcmVkUmVzb3VyY2UuQ29uZGl0aW9uUmVzb3VyY2UoQ29uZGl0aW9ucywNCiAge0ZISVIuY2Fub25pY2Fse3ZhbHVlOiAnaHR0cDovL3d3dy5jZGMuZ292L25oc24vZmhpcnBvcnRhbC9kcW0vaWcvU3RydWN0dXJlRGVmaW5pdGlvbi9hY2gtY29uZGl0aW9uJ319KQ0KDQpkZWZpbmUgIlNERSBEZXZpY2UiOg0KICBbRGV2aWNlXSBEZXZpY2VzIA0KICB3aGVyZSBleGlzdHMoIkluaXRpYWwgUG9wdWxhdGlvbiIpDQogIHJldHVybiBEZXZpY2VSZXNvdXJjZShEZXZpY2VzLA0KICB7RkhJUi5jYW5vbmljYWx7dmFsdWU6ICdodHRwOi8vd3d3LmNkYy5nb3Yvbmhzbi9maGlycG9ydGFsL2RxbS9pZy9TdHJ1Y3R1cmVEZWZpbml0aW9uL2FjaC1kZXZpY2UnfX0pDQoNCmRlZmluZSAiU0RFIERpYWdub3N0aWNSZXBvcnQgTGFiIjoNCiAgIkRpYWdub3N0aWNSZXBvcnRzIiBEaWFnbm9zdGljUmVwb3J0cw0KICB3aGVyZSAoZXhpc3RzKERpYWdub3N0aWNSZXBvcnRzLmNhdGVnb3J5IENhdGVnb3J5IHdoZXJlIENhdGVnb3J5IH4gIkxBQiIpDQogICAgYW5kIGV4aXN0cygNCiAgICAgICJJbml0aWFsIFBvcHVsYXRpb24iIElQDQogICAgICB3aGVyZSBHbG9iYWwuIk5vcm1hbGl6ZSBJbnRlcnZhbCIoRGlhZ25vc3RpY1JlcG9ydHMuZWZmZWN0aXZlKSBvdmVybGFwcyBJUC5wZXJpb2QpKQ0KICByZXR1cm4gU2hhcmVkUmVzb3VyY2UuRGlhZ25vc3RpY1JlcG9ydExhYlJlc291cmNlKERpYWdub3N0aWNSZXBvcnRzLA0KICB7RkhJUi5jYW5vbmljYWx7dmFsdWU6ICdodHRwOi8vd3d3LmNkYy5nb3Yvbmhzbi9maGlycG9ydGFsL2RxbS9pZy9TdHJ1Y3R1cmVEZWZpbml0aW9uL2FjaC1kaWFnbm9zdGljcmVwb3J0LWxhYid9fSkNCiANCmRlZmluZSAiU0RFIERpYWdub3N0aWNSZXBvcnQgTm90ZSI6DQogICJEaWFnbm9zdGljUmVwb3J0cyIgRGlhZ25vc3RpY1JlcG9ydHMNCiAgd2hlcmUgKChleGlzdHMoRGlhZ25vc3RpY1JlcG9ydHMuY2F0ZWdvcnkgQ2F0ZWdvcnkgd2hlcmUgQ2F0ZWdvcnkgfiAiUmFkaW9sb2d5IikpDQogICAgb3IgZXhpc3RzKChEaWFnbm9zdGljUmVwb3J0cy5jYXRlZ29yeSBDYXRlZ29yeSB3aGVyZSBDYXRlZ29yeSB+ICJQYXRob2xvZ3kiKSkNCiAgICBvciBleGlzdHMoKERpYWdub3N0aWNSZXBvcnRzLmNhdGVnb3J5IENhdGVnb3J5IHdoZXJlIENhdGVnb3J5IH4gIkNhcmRpb2xvZ3kiKSkpDQogICAgYW5kIGV4aXN0cygNCiAgICAgICJJbml0aWFsIFBvcHVsYXRpb24iIElQDQogICAgICB3aGVyZSBHbG9iYWwuIk5vcm1hbGl6ZSBJbnRlcnZhbCIoRGlhZ25vc3RpY1JlcG9ydHMuZWZmZWN0aXZlKSBvdmVybGFwcyBJUC5wZXJpb2QpDQogIHJldHVybiBEaWFnbm9zdGljUmVwb3J0UmVzb3VyY2UoRGlhZ25vc3RpY1JlcG9ydHMsDQogIHtGSElSLmNhbm9uaWNhbHt2YWx1ZTogJ2h0dHA6Ly93d3cuY2RjLmdvdi9uaHNuL2ZoaXJwb3J0YWwvZHFtL2lnL1N0cnVjdHVyZURlZmluaXRpb24vYWNoLWRpYWdub3N0aWNyZXBvcnQtbm90ZSd9fSkNCg0KZGVmaW5lICJTREUgRGlhZ25vc3RpY1JlcG9ydCBPdGhlcnMiOg0KICBbRGlhZ25vc3RpY1JlcG9ydF0gRGlhZ25vc3RpY1JlcG9ydHMNCiAgd2hlcmUgbm90ICgoZXhpc3RzKERpYWdub3N0aWNSZXBvcnRzLmNhdGVnb3J5IENhdGVnb3J5IHdoZXJlIENhdGVnb3J5IH4gIlJhZGlvbG9neSIpKQ0KICAgIG9yIGV4aXN0cygoRGlhZ25vc3RpY1JlcG9ydHMuY2F0ZWdvcnkgQ2F0ZWdvcnkgd2hlcmUgQ2F0ZWdvcnkgfiAiUGF0aG9sb2d5IikpDQogICAgb3IgZXhpc3RzKChEaWFnbm9zdGljUmVwb3J0cy5jYXRlZ29yeSBDYXRlZ29yeSB3aGVyZSBDYXRlZ29yeSB+ICJDYXJkaW9sb2d5IikpDQogICAgb3IgZXhpc3RzKERpYWdub3N0aWNSZXBvcnRzLmNhdGVnb3J5IENhdGVnb3J5IHdoZXJlIENhdGVnb3J5IH4gIkxBQiIpKQ0KICAgIGFuZCBleGlzdHMoIkluaXRpYWwgUG9wdWxhdGlvbiIgSVANCiAgICAgIHdoZXJlIEdsb2JhbC4iTm9ybWFsaXplIEludGVydmFsIihEaWFnbm9zdGljUmVwb3J0cy5lZmZlY3RpdmUpIG92ZXJsYXBzIElQLnBlcmlvZCkNCiAgcmV0dXJuIERpYWdub3N0aWNSZXBvcnRSZXNvdXJjZShEaWFnbm9zdGljUmVwb3J0cywNCiAge0ZISVIuY2Fub25pY2Fse3ZhbHVlOiAnaHR0cDovL3d3dy5jZGMuZ292L25oc24vZmhpcnBvcnRhbC9kcW0vaWcvU3RydWN0dXJlRGVmaW5pdGlvbi9hY2gtZGlhZ25vc3RpY3JlcG9ydCd9fSkNCg0KZGVmaW5lICJTREUgRW5jb3VudGVyIjogDQogICJFbmNvdW50ZXJzIiBFbmNvdW50ZXJzDQogIHdoZXJlIGV4aXN0cygNCiAgICAiSW5pdGlhbCBQb3B1bGF0aW9uIiBJUA0KICAgIHdoZXJlIEVuY291bnRlcnMucGVyaW9kIG92ZXJsYXBzIElQLnBlcmlvZCkNCiAgcmV0dXJuIEVuY291bnRlclJlc291cmNlKEVuY291bnRlcnMsDQogIHtGSElSLmNhbm9uaWNhbHt2YWx1ZTogJ2h0dHA6Ly93d3cuY2RjLmdvdi9uaHNuL2ZoaXJwb3J0YWwvZHFtL2lnL1N0cnVjdHVyZURlZmluaXRpb24vYWNoLWVuY291bnRlcid9fSkNCg0KZGVmaW5lICJTREUgTG9jYXRpb24iOg0KICAiR2V0IExvY2F0aW9ucyBmcm9tIElQIEVuY291bnRlcnMgaW4gTWVhc3VyZW1lbnQgUGVyaW9kIiBMb2NhdGlvbnMNCiAgd2hlcmUgZXhpc3RzKCJJbml0aWFsIFBvcHVsYXRpb24iKQ0KICBhbmQgTG9jYXRpb25zIGlzIG5vdCBudWxsDQogIHJldHVybiBTaGFyZWRSZXNvdXJjZS5Mb2NhdGlvblJlc291cmNlKExvY2F0aW9ucywNCiAge0ZISVIuY2Fub25pY2Fse3ZhbHVlOiAnaHR0cDovL3d3dy5jZGMuZ292L25oc24vZmhpcnBvcnRhbC9kcW0vaWcvU3RydWN0dXJlRGVmaW5pdGlvbi9hY2gtbG9jYXRpb24nfX0pDQogDQpkZWZpbmUgIlNERSBNZWRpY2F0aW9uIEFkbWluaXN0cmF0aW9uIjoNCiAgW01lZGljYXRpb25BZG1pbmlzdHJhdGlvbl0gTWVkaWNhdGlvbkFkbWluaXN0cmF0aW9ucyANCiAgd2hlcmUgZXhpc3RzKA0KICAgICJJbml0aWFsIFBvcHVsYXRpb24iIElQDQogICAgd2hlcmUgR2xvYmFsLiJOb3JtYWxpemUgSW50ZXJ2YWwiKE1lZGljYXRpb25BZG1pbmlzdHJhdGlvbnMuZWZmZWN0aXZlKSBvdmVybGFwcyBJUC5wZXJpb2QpDQogIHJldHVybiBTaGFyZWRSZXNvdXJjZS5NZWRpY2F0aW9uQWRtaW5pc3RyYXRpb25SZXNvdXJjZShNZWRpY2F0aW9uQWRtaW5pc3RyYXRpb25zLA0KICB7RkhJUi5jYW5vbmljYWx7dmFsdWU6ICdodHRwOi8vd3d3LmNkYy5nb3Yvbmhzbi9maGlycG9ydGFsL2RxbS9pZy9TdHJ1Y3R1cmVEZWZpbml0aW9uL2FjaC1tZWRpY2F0aW9uYWRtaW5pc3RyYXRpb24nfX0pDQogDQpkZWZpbmUgIlNERSBNZWRpY2F0aW9uIFJlcXVlc3QiOg0KICBbTWVkaWNhdGlvblJlcXVlc3RdIE1lZGljYXRpb25SZXF1ZXN0cyANCiAgd2hlcmUgZXhpc3RzKA0KICAgICJJbml0aWFsIFBvcHVsYXRpb24iIElQDQogICAgd2hlcmUgTWVkaWNhdGlvblJlcXVlc3RzLmF1dGhvcmVkT24gZHVyaW5nIElQLnBlcmlvZCkNCiAgcmV0dXJuIFNoYXJlZFJlc291cmNlLk1lZGljYXRpb25SZXF1ZXN0UmVzb3VyY2UoTWVkaWNhdGlvblJlcXVlc3RzLA0KICB7RkhJUi5jYW5vbmljYWx7dmFsdWU6ICdodHRwOi8vd3d3LmNkYy5nb3Yvbmhzbi9maGlycG9ydGFsL2RxbS9pZy9TdHJ1Y3R1cmVEZWZpbml0aW9uL2FjaC1tZWRpY2F0aW9ucmVxdWVzdCd9fSkNCg0KZGVmaW5lICJTREUgTWVkaWNhdGlvbiI6DQogICgiU0RFIE1lZGljYXRpb24gUmVxdWVzdCINCiAgdW5pb24gIlNERSBNZWRpY2F0aW9uIEFkbWluaXN0cmF0aW9uIikgTWVkUmVxT3JBZG1pbg0KICB3aGVyZSBNZWRSZXFPckFkbWluLm1lZGljYXRpb24gaXMgRkhJUi5SZWZlcmVuY2UNCiAgYW5kIGV4aXN0cygiSW5pdGlhbCBQb3B1bGF0aW9uIikgLy9ObyBsb25nZXIgbmVlZCB0byBjaGVjayBmb3IgdGltaW5nIGhlcmUgYmVjYXVzZSBpdCdzIGNoZWNrZWQgaW4gU0RFIE1lZGljYXRpb24gUmVxdWVzdC9BZG1pbmlzdHJpYXRpb24NCiAgcmV0dXJuIFNoYXJlZFJlc291cmNlLk1lZGljYXRpb25SZXNvdXJjZShHZXRNZWRpY2F0aW9uRnJvbShNZWRSZXFPckFkbWluLm1lZGljYXRpb24pLA0KICB7RkhJUi5jYW5vbmljYWx7dmFsdWU6ICdodHRwOi8vd3d3LmNkYy5nb3Yvbmhzbi9maGlycG9ydGFsL2RxbS9pZy9TdHJ1Y3R1cmVEZWZpbml0aW9uL2FjaC1tZWRpY2F0aW9uJ319KQ0KDQpkZWZpbmUgIlNERSBPYnNlcnZhdGlvbiBMYWIgQ2F0ZWdvcnkiOg0KICAiT2JzZXJ2YXRpb25zIiBPYnNlcnZhdGlvbnMgDQogIHdoZXJlIChleGlzdHMoT2JzZXJ2YXRpb25zLmNhdGVnb3J5IENhdGVnb3J5IHdoZXJlIENhdGVnb3J5IH4gImxhYm9yYXRvcnkiKSkNCiAgICBhbmQgZXhpc3RzKA0KICAgICAgIkluaXRpYWwgUG9wdWxhdGlvbiIgSVANCiAgICAgIHdoZXJlIEdsb2JhbC4iTm9ybWFsaXplIEludGVydmFsIihPYnNlcnZhdGlvbnMuZWZmZWN0aXZlKSBvdmVybGFwcyBJUC5wZXJpb2QpDQogIHJldHVybiBTaGFyZWRSZXNvdXJjZS5PYnNlcnZhdGlvbkxhYlJlc291cmNlKE9ic2VydmF0aW9ucywNCiAge0ZISVIuY2Fub25pY2Fse3ZhbHVlOiAnaHR0cDovL3d3dy5jZGMuZ292L25oc24vZmhpcnBvcnRhbC9kcW0vaWcvU3RydWN0dXJlRGVmaW5pdGlvbi9hY2gtb2JzZXJ2YXRpb24tbGFiJ319KQ0KDQovL1ZpdGFsIFNpZ25zIE9ic2VydmF0aW9uIGhhcyBpdHMgb3duIHByb2ZpbGUgaW4gRkhJUiBCYXNlDQpkZWZpbmUgIlNERSBPYnNlcnZhdGlvbiBWaXRhbCBTaWducyBDYXRlZ29yeSI6DQogICJPYnNlcnZhdGlvbnMiIE9ic2VydmF0aW9ucyANCiAgd2hlcmUgKGV4aXN0cyhPYnNlcnZhdGlvbnMuY2F0ZWdvcnkgQ2F0ZWdvcnkgd2hlcmUgQ2F0ZWdvcnkgfiAidml0YWwtc2lnbnMiKSkNCiAgICBhbmQgZXhpc3RzKA0KICAgICAgIkluaXRpYWwgUG9wdWxhdGlvbiIgSVANCiAgICAgIHdoZXJlIEdsb2JhbC4iTm9ybWFsaXplIEludGVydmFsIihPYnNlcnZhdGlvbnMuZWZmZWN0aXZlKSBvdmVybGFwcyBJUC5wZXJpb2QpDQogIHJldHVybiBPYnNlcnZhdGlvblZpdGFsU2lnbnNSZXNvdXJjZShPYnNlcnZhdGlvbnMsDQogIHtGSElSLmNhbm9uaWNhbHt2YWx1ZTogJ2h0dHA6Ly93d3cuY2RjLmdvdi9uaHNuL2ZoaXJwb3J0YWwvZHFtL2lnL1N0cnVjdHVyZURlZmluaXRpb24vYWNoLW9ic2VydmF0aW9uLXZpdGFscyd9fSkNCg0KLy9EZWZhdWx0aW5nIHRvIGJhc2UgRkhJUiBwcm9maWxlIGFzIHRoZXJlIGFyZSBubyBpbmRpdmlkdWFsIHByb2ZpbGVzIGluIFVTIENvcmUgMy4xLjEgdGhhdCBjb3ZlciB0aGVzZSBPYnNlcnZhdGlvbiBjYXRlZ29yaWVzDQpkZWZpbmUgIlNERSBPYnNlcnZhdGlvbiBDYXRlZ29yeSI6DQogICJPYnNlcnZhdGlvbnMiIE9ic2VydmF0aW9ucyANCiAgd2hlcmUgKChleGlzdHMoT2JzZXJ2YXRpb25zLmNhdGVnb3J5IENhdGVnb3J5IHdoZXJlIENhdGVnb3J5IH4gInNvY2lhbC1oaXN0b3J5IikpDQogICAgb3IgKGV4aXN0cyhPYnNlcnZhdGlvbnMuY2F0ZWdvcnkgQ2F0ZWdvcnkgd2hlcmUgQ2F0ZWdvcnkgfiAic3VydmV5IikpDQogICAgb3IgKGV4aXN0cyhPYnNlcnZhdGlvbnMuY2F0ZWdvcnkgQ2F0ZWdvcnkgd2hlcmUgQ2F0ZWdvcnkgfiAiaW1hZ2luZyIpKQ0KICAgIG9yIChleGlzdHMoT2JzZXJ2YXRpb25zLmNhdGVnb3J5IENhdGVnb3J5IHdoZXJlIENhdGVnb3J5IH4gInByb2NlZHVyZSIpKSkNCiAgICBhbmQgZXhpc3RzKA0KICAgICAgIkluaXRpYWwgUG9wdWxhdGlvbiIgSVANCiAgICAgIHdoZXJlIEdsb2JhbC4iTm9ybWFsaXplIEludGVydmFsIihPYnNlcnZhdGlvbnMuZWZmZWN0aXZlKSBvdmVybGFwcyBJUC5wZXJpb2QpDQogIHJldHVybiBPYnNlcnZhdGlvblJlc291cmNlKE9ic2VydmF0aW9ucywNCiAge0ZISVIuY2Fub25pY2Fse3ZhbHVlOiAnaHR0cDovL3d3dy5jZGMuZ292L25oc24vZmhpcnBvcnRhbC9kcW0vaWcvU3RydWN0dXJlRGVmaW5pdGlvbi9hY2gtb2JzZXJ2YXRpb24nfX0pDQoNCmRlZmluZSAiU0RFIENvdmVyYWdlIjogDQoJW0NvdmVyYWdlXSBDb3ZlcmFnZXMNCiAgd2hlcmUgZXhpc3RzKA0KICAgICJJbml0aWFsIFBvcHVsYXRpb24iIElQDQogICAgd2hlcmUgQ292ZXJhZ2VzLnBlcmlvZCBvdmVybGFwcyBJUC5wZXJpb2QpDQogIHJldHVybiBTaGFyZWRSZXNvdXJjZS5Db3ZlcmFnZVJlc291cmNlKENvdmVyYWdlcywNCiAge0ZISVIuY2Fub25pY2Fse3ZhbHVlOiAnaHR0cDovL3d3dy5jZGMuZ292L25oc24vZmhpcnBvcnRhbC9kcW0vaWcvU3RydWN0dXJlRGVmaW5pdGlvbi9hY2gtY292ZXJhZ2UnfX0pDQoNCmRlZmluZSAiU0RFIFByb2NlZHVyZSI6DQogIFtQcm9jZWR1cmVdIFByb2NlZHVyZXMgDQogIHdoZXJlIGV4aXN0cygNCiAgICAiSW5pdGlhbCBQb3B1bGF0aW9uIiBJUA0KICAgIHdoZXJlIEdsb2JhbC4iTm9ybWFsaXplIEludGVydmFsIihQcm9jZWR1cmVzLnBlcmZvcm1lZCkgb3ZlcmxhcHMgSVAucGVyaW9kKQ0KICByZXR1cm4gU2hhcmVkUmVzb3VyY2UuUHJvY2VkdXJlUmVzb3VyY2UoUHJvY2VkdXJlcywNCiAge0ZISVIuY2Fub25pY2Fse3ZhbHVlOiAnaHR0cDovL3d3dy5jZGMuZ292L25oc24vZmhpcnBvcnRhbC9kcW0vaWcvU3RydWN0dXJlRGVmaW5pdGlvbi9hY2gtcHJvY2VkdXJlJ319KQ0KDQpkZWZpbmUgIlNERSBTcGVjaW1lbiI6DQogIFtTcGVjaW1lbl0gU3BlY2ltZW5zDQogIHdoZXJlIGV4aXN0cygNCiAgICAiSW5pdGlhbCBQb3B1bGF0aW9uIiBJUA0KICAgIHdoZXJlIEdsb2JhbC4iTm9ybWFsaXplIEludGVydmFsIihTcGVjaW1lbnMuY29sbGVjdGlvbi5jb2xsZWN0ZWQpIG92ZXJsYXBzIElQLnBlcmlvZA0KICApDQogIHJldHVybiBTaGFyZWRSZXNvdXJjZS5TcGVjaW1lblJlc291cmNlKFNwZWNpbWVucywNCiAge0ZISVIuY2Fub25pY2Fse3ZhbHVlOiAnaHR0cDovL3d3dy5jZGMuZ292L25oc24vZmhpcnBvcnRhbC9kcW0vaWcvU3RydWN0dXJlRGVmaW5pdGlvbi9hY2gtc3BlY2ltZW4nfX0pDQoNCmRlZmluZSAiU0RFIFNlcnZpY2UgUmVxdWVzdCI6DQogIFtTZXJ2aWNlUmVxdWVzdF0gU2VydmljZVJlcXVlc3RzDQogIHdoZXJlIGV4aXN0cygiSW5pdGlhbCBQb3B1bGF0aW9uIiBJUA0KICAgIHdoZXJlIFNlcnZpY2VSZXF1ZXN0cy5hdXRob3JlZE9uIGR1cmluZyBJUC5wZXJpb2QpDQogIHJldHVybiBTaGFyZWRSZXNvdXJjZS5TZXJ2aWNlUmVxdWVzdFJlc291cmNlKFNlcnZpY2VSZXF1ZXN0cywNCiAge0ZISVIuY2Fub25pY2Fse3ZhbHVlOiAnaHR0cDovL3d3dy5jZGMuZ292L25oc24vZmhpcnBvcnRhbC9kcW0vaWcvU3RydWN0dXJlRGVmaW5pdGlvbi9hY2gtc2VydmljZXJlcXVlc3QnfX0pDQoNCmRlZmluZSAiU0RFIE1pbmltYWwgUGF0aWVudCI6DQogIFBhdGllbnQgcA0KICByZXR1cm4gU2hhcmVkUmVzb3VyY2UuUGF0aWVudFJlc291cmNlKHAsDQogIHtGSElSLmNhbm9uaWNhbHt2YWx1ZTogJ2h0dHA6Ly93d3cuY2RjLmdvdi9uaHNuL2ZoaXJwb3J0YWwvZHFtL2lnL1N0cnVjdHVyZURlZmluaXRpb24vY3Jvc3MtbWVhc3VyZS1wYXRpZW50J319KQ0KDQovLw0KLy9GdW5jdGlvbnMNCi8vDQpkZWZpbmUgZnVuY3Rpb24gIkdldE1lZGljYXRpb25Gcm9tIihjaG9pY2UgQ2hvaWNlPEZISVIuQ29kZWFibGVDb25jZXB0LCBGSElSLlJlZmVyZW5jZT4pOg0KICBjYXNlDQogICAgd2hlbiBjaG9pY2UgaXMgRkhJUi5SZWZlcmVuY2UgdGhlbg0KICAgICAgR2V0TWVkaWNhdGlvbihjaG9pY2UgYXMgRkhJUi5SZWZlcmVuY2UpDQogICAgZWxzZQ0KICAgICAgbnVsbA0KICBlbmQNCg0KZGVmaW5lIGZ1bmN0aW9uICJHZXRNZWRpY2F0aW9uIihyZWZlcmVuY2UgUmVmZXJlbmNlKToNCiAgc2luZ2xldG9uIGZyb20gKA0KICAgIFtNZWRpY2F0aW9uXSBNZWRpY2F0aW9ucw0KICAgIHdoZXJlIE1lZGljYXRpb25zLmlkID0gR2xvYmFsLkdldElkKHJlZmVyZW5jZS5yZWZlcmVuY2UpDQogICkNCg0KZGVmaW5lIGZ1bmN0aW9uICJHZXRDb25kaXRpb24iKHJlZmVyZW5jZSBSZWZlcmVuY2UpOg0KICBzaW5nbGV0b24gZnJvbSAoDQogICAgIkNvbmRpdGlvbnMiIENvbmRpdGlvbnMNCiAgICB3aGVyZSBDb25kaXRpb25zLmlkID0gR2xvYmFsLkdldElkKHJlZmVyZW5jZS5yZWZlcmVuY2UpDQogICkNCg0KZGVmaW5lIGZ1bmN0aW9uICJHZXRFbmNvdW50ZXIiKHJlZmVyZW5jZSBSZWZlcmVuY2UpOg0KICBzaW5nbGV0b24gZnJvbSAoDQogICAgIkVuY291bnRlcnMiIEVuY291bnRlcnMNCiAgICB3aGVyZSBFbmNvdW50ZXJzLmlkID0gR2xvYmFsLkdldElkKHJlZmVyZW5jZS5yZWZlcmVuY2UpDQogICkNCg0KLy8NCi8vTWVhc3VyZSBTcGVjaWZpYyBSZXNvdXJjZSBDcmVhdGlvbiBGdW5jdGlvbnMNCi8vDQpkZWZpbmUgZnVuY3Rpb24gRGV2aWNlVWRpQ2Fycmllcih1ZGlDYXJyaWVyIExpc3Q8RkhJUi5EZXZpY2UuVWRpQ2Fycmllcj4pOg0KICB1ZGlDYXJyaWVyIHUNCiAgcmV0dXJuIEZISVIuRGV2aWNlLlVkaUNhcnJpZXJ7DQogICAgZGV2aWNlSWRlbnRpZmllcjogdS5kZXZpY2VJZGVudGlmaWVyLA0KICAgIGlzc3VlcjogdS5pc3N1ZXIsDQogICAganVyaXNkaWN0aW9uOiB1Lmp1cmlzZGljdGlvbiwNCiAgICBjYXJyaWVyQUlEQzogdS5jYXJyaWVyQUlEQywNCiAgICBjYXJyaWVySFJGOiB1LmNhcnJpZXJIUkYsDQogICAgZW50cnlUeXBlOiB1LmVudHJ5VHlwZQ0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBEZXZpY2VEZXZpY2VOYW1lKGRldmljZU5hbWUgTGlzdDxGSElSLkRldmljZS5EZXZpY2VOYW1lPik6DQogIGRldmljZU5hbWUgZA0KICByZXR1cm4gRkhJUi5EZXZpY2UuRGV2aWNlTmFtZXsNCiAgICBuYW1lOiBkLm5hbWUsDQogICAgdHlwZTogZC50eXBlDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIERldmljZVNwZWNpYWxpemF0aW9uKHNwZWNpYWxpemF0aW9uIExpc3Q8RkhJUi5EZXZpY2UuU3BlY2lhbGl6YXRpb24+KToNCiAgc3BlY2lhbGl6YXRpb24gcw0KICByZXR1cm4gRkhJUi5EZXZpY2UuU3BlY2lhbGl6YXRpb257DQogICAgc3lzdGVtVHlwZTogcy5zeXN0ZW1UeXBlLA0KICAgIHZlcnNpb246IHMudmVyc2lvbg0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBEZXZpY2VWZXJzaW9uKHZlcnNpb24gTGlzdDxGSElSLkRldmljZS5WZXJzaW9uPik6DQogIHZlcnNpb24gdg0KICByZXR1cm4gRkhJUi5EZXZpY2UuVmVyc2lvbnsNCiAgICB0eXBlOiB2LnR5cGUsDQogICAgY29tcG9uZW50OiB2LmNvbXBvbmVudCwNCiAgICB2YWx1ZTogdi52YWx1ZQ0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBEZXZpY2VQcm9wZXJ0eShkZXZpY2VQcm9wZXJ0eSBMaXN0PEZISVIuRGV2aWNlLlByb3BlcnR5Pik6DQogIGRldmljZVByb3BlcnR5IGQNCiAgcmV0dXJuIEZISVIuRGV2aWNlLlByb3BlcnR5ew0KICAgIGlkOiBkLmlkLA0KICAgIHR5cGU6IGQudHlwZSwNCiAgICB2YWx1ZVF1YW50aXR5OiBkLnZhbHVlUXVhbnRpdHksDQogICAgdmFsdWVDb2RlOiBkLnZhbHVlQ29kZQ0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBEZXZpY2VSZXNvdXJjZShkZXZpY2UgRGV2aWNlLCBwcm9maWxlVVJMcyBMaXN0PEZISVIuY2Fub25pY2FsPik6DQogIGRldmljZSBkDQogIHJldHVybiBEZXZpY2V7DQogICAgaWQ6IEZISVIuaWR7dmFsdWU6ICdMQ1ItJyArIGQuaWR9LA0KICAgIG1ldGE6IFNoYXJlZFJlc291cmNlLk1ldGFFbGVtZW50KGQsIHByb2ZpbGVVUkxzKSwNCiAgICBleHRlbnNpb246IGQuZXh0ZW5zaW9uLA0KICAgIGRlZmluaXRpb246IGQuZGVmaW5pdGlvbiwNCiAgICB1ZGlDYXJyaWVyOiBEZXZpY2VVZGlDYXJyaWVyKGQudWRpQ2FycmllciksDQogICAgc3RhdHVzOiBkLnN0YXR1cywNCiAgICBzdGF0dXNSZWFzb246IGQuc3RhdHVzUmVhc29uLA0KICAgIGRpc3RpbmN0SWRlbnRpZmllcjogZC5kaXN0aW5jdElkZW50aWZpZXIsDQogICAgbWFudWZhY3R1cmVyOiBkLm1hbnVmYWN0dXJlciwNCiAgICBtYW51ZmFjdHVyZURhdGU6IGQubWFudWZhY3R1cmVEYXRlLA0KICAgIGV4cGlyYXRpb25EYXRlOiBkLmV4cGlyYXRpb25EYXRlLA0KICAgIGxvdE51bWJlcjogZC5sb3ROdW1iZXIsDQogICAgc2VyaWFsTnVtYmVyOiBkLnNlcmlhbE51bWJlciwNCiAgICBkZXZpY2VOYW1lOiBEZXZpY2VEZXZpY2VOYW1lKGQuZGV2aWNlTmFtZSksDQogICAgbW9kZWxOdW1iZXI6IGQubW9kZWxOdW1iZXIsDQogICAgcGFydE51bWJlcjogZC5wYXJ0TnVtYmVyLA0KICAgIHR5cGU6IGQudHlwZSwNCiAgICBzcGVjaWFsaXphdGlvbjogRGV2aWNlU3BlY2lhbGl6YXRpb24oZC5zcGVjaWFsaXphdGlvbiksDQogICAgdmVyc2lvbjogRGV2aWNlVmVyc2lvbihkLnZlcnNpb24pLA0KICAgIHByb3BlcnR5OiBEZXZpY2VQcm9wZXJ0eShkLnByb3BlcnR5KSwNCiAgICBwYXRpZW50OiBkLnBhdGllbnQsDQogICAgb3duZXI6IGQub3duZXIsDQogICAgY29udGFjdDogZC5jb250YWN0LA0KICAgIGxvY2F0aW9uOiBkLmxvY2F0aW9uLA0KICAgIHVybDogZC51cmwsDQogICAgbm90ZTogZC5ub3RlLA0KICAgIHNhZmV0eTogZC5zYWZldHksDQogICAgcGFyZW50OiBkLnBhcmVudA0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBEaWFnbm9zdGljUmVwb3J0UmVzb3VyY2UoZGlhZ25vc3RpY1JlcG9ydCBEaWFnbm9zdGljUmVwb3J0LCBwcm9maWxlVVJMcyBMaXN0PEZISVIuY2Fub25pY2FsPik6DQogIGRpYWdub3N0aWNSZXBvcnQgZA0KICByZXR1cm4gRGlhZ25vc3RpY1JlcG9ydHsNCiAgICBpZDogRkhJUi5pZHt2YWx1ZTogJ0xDUi0nICsgZC5pZH0sDQogICAgbWV0YTogU2hhcmVkUmVzb3VyY2UuTWV0YUVsZW1lbnQoZCwgcHJvZmlsZVVSTHMpLA0KICAgIGV4dGVuc2lvbjogZC5leHRlbnNpb24sDQogICAgYmFzZWRPbjogZC5iYXNlZE9uLA0KICAgIHN0YXR1czogZC5zdGF0dXMsDQogICAgY2F0ZWdvcnk6IGQuY2F0ZWdvcnksDQogICAgY29kZTogZC5jb2RlLA0KICAgIHN1YmplY3Q6IGQuc3ViamVjdCwNCiAgICBlbmNvdW50ZXI6IGQuZW5jb3VudGVyLA0KICAgIGVmZmVjdGl2ZTogZC5lZmZlY3RpdmUsDQogICAgaXNzdWVkOiBkLmlzc3VlZCwNCiAgICBwZXJmb3JtZXI6IGQucGVyZm9ybWVyLA0KICAgIHJlc3VsdHNJbnRlcnByZXRlcjogZC5yZXN1bHRzSW50ZXJwcmV0ZXIsDQogICAgc3BlY2ltZW46IGQuc3BlY2ltZW4sDQogICAgcmVzdWx0OiBkLnJlc3VsdCwNCiAgICBjb25jbHVzaW9uOiBkLmNvbmNsdXNpb24sDQogICAgY29uY2x1c2lvbkNvZGU6IGQuY29uY2x1c2lvbkNvZGUNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gRW5jb3VudGVyUmVzb3VyY2UoZW5jb3VudGVyIEVuY291bnRlciwgcHJvZmlsZVVSTHMgTGlzdDxGSElSLmNhbm9uaWNhbD4pOg0KICBlbmNvdW50ZXIgZQ0KICByZXR1cm4gRW5jb3VudGVyew0KICAgIGlkOiBGSElSLmlke3ZhbHVlOiAnTENSLScgKyBlLmlkfSwNCiAgICBtZXRhOiBTaGFyZWRSZXNvdXJjZS5NZXRhRWxlbWVudChlLCBwcm9maWxlVVJMcyksDQogICAgZXh0ZW5zaW9uOiBlLmV4dGVuc2lvbiwNCiAgICBpZGVudGlmaWVyOiBTaGFyZWRSZXNvdXJjZS5FbmNvdW50ZXJJZGVudGlmaWVyKGUuaWRlbnRpZmllciksDQogICAgc3RhdHVzOiBlLnN0YXR1cywNCiAgICBzdGF0dXNIaXN0b3J5OiBTaGFyZWRSZXNvdXJjZS5FbmNvdW50ZXJTdGF0dXNIaXN0b3J5KGUuc3RhdHVzSGlzdG9yeSksDQogICAgY2xhc3M6IGUuY2xhc3MsDQogICAgY2xhc3NIaXN0b3J5OiBTaGFyZWRSZXNvdXJjZS5FbmNvdW50ZXJDbGFzc0hpc3RvcnkoZS5jbGFzc0hpc3RvcnkpLA0KICAgIHR5cGU6IGUudHlwZSwNCiAgICBzZXJ2aWNlVHlwZTogZS5zZXJ2aWNlVHlwZSwNCiAgICBwcmlvcml0eTogZS5wcmlvcml0eSwNCiAgICBzdWJqZWN0OiBlLnN1YmplY3QsDQogICAgcGVyaW9kOiBlLnBlcmlvZCwNCiAgICBsZW5ndGg6IGUubGVuZ3RoLA0KICAgIHJlYXNvbkNvZGU6IGUucmVhc29uQ29kZSwNCiAgICByZWFzb25SZWZlcmVuY2U6IGUucmVhc29uUmVmZXJlbmNlLA0KICAgIGRpYWdub3NpczogU2hhcmVkUmVzb3VyY2UuRW5jb3VudGVyRGlhZ25vc2lzKGUuZGlhZ25vc2lzKSwNCiAgICBhY2NvdW50OiBlLmFjY291bnQsDQogICAgaG9zcGl0YWxpemF0aW9uOiBTaGFyZWRSZXNvdXJjZS5FbmNvdW50ZXJIb3NwaXRhbGl6YXRpb24oZS5ob3NwaXRhbGl6YXRpb24pLA0KICAgIGxvY2F0aW9uOiBTaGFyZWRSZXNvdXJjZS5FbmNvdW50ZXJMb2NhdGlvbihlLmxvY2F0aW9uKSwNCiAgICBwYXJ0T2Y6IGUucGFydE9mDQogIH0NCg0KZGVmaW5lIGZ1bmN0aW9uIE9ic2VydmF0aW9uUmVzb3VyY2Uob2JzZXJ2YXRpb24gT2JzZXJ2YXRpb24sIHByb2ZpbGVVUkxzIExpc3Q8RkhJUi5jYW5vbmljYWw+KToNCiAgb2JzZXJ2YXRpb24gbw0KICByZXR1cm4gT2JzZXJ2YXRpb257DQogICAgaWQ6IEZISVIuaWQge3ZhbHVlOiAnTENSLScgKyBvLmlkfSwNCiAgICBtZXRhOiBTaGFyZWRSZXNvdXJjZS5NZXRhRWxlbWVudChvLCBwcm9maWxlVVJMcyksDQogICAgZXh0ZW5zaW9uOiBvLmV4dGVuc2lvbiwNCiAgICBwYXJ0T2Y6IG8ucGFydE9mLA0KICAgIHN0YXR1czogby5zdGF0dXMsDQogICAgY2F0ZWdvcnk6IG8uY2F0ZWdvcnksDQogICAgY29kZTogby5jb2RlLA0KICAgIHN1YmplY3Q6IG8uc3ViamVjdCwNCiAgICBmb2N1czogby5mb2N1cywNCiAgICBlbmNvdW50ZXI6IG8uZW5jb3VudGVyLA0KICAgIGVmZmVjdGl2ZTogby5lZmZlY3RpdmUsDQogICAgaXNzdWVkOiBvLmlzc3VlZCwNCiAgICBwZXJmb3JtZXI6IG8ucGVyZm9ybWVyLA0KICAgIHZhbHVlOiBvLnZhbHVlLA0KICAgIGRhdGFBYnNlbnRSZWFzb246IG8uZGF0YUFic2VudFJlYXNvbiwNCiAgICBpbnRlcnByZXRhdGlvbjogby5pbnRlcnByZXRhdGlvbiwNCiAgICBub3RlOiBvLm5vdGUsDQogICAgYm9keVNpdGU6IG8uYm9keVNpdGUsDQogICAgbWV0aG9kOiBvLm1ldGhvZCwNCiAgICBzcGVjaW1lbjogby5zcGVjaW1lbiwNCiAgICBkZXZpY2U6IG8uZGV2aWNlLA0KICAgIHJlZmVyZW5jZVJhbmdlOiBTaGFyZWRSZXNvdXJjZS5PYnNlcnZhdGlvblJlZmVyZW5jZVJhbmdlKG8ucmVmZXJlbmNlUmFuZ2UpLA0KICAgIGhhc01lbWJlcjogby5oYXNNZW1iZXIsDQogICAgZGVyaXZlZEZyb206IG8uZGVyaXZlZEZyb20sDQogICAgY29tcG9uZW50OiBTaGFyZWRSZXNvdXJjZS5PYnNlcnZhdGlvbkNvbXBvbmVudChvLmNvbXBvbmVudCkNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gT2JzZXJ2YXRpb25WaXRhbFNpZ25zQ29kaW5nKGNvZGluZyBMaXN0PENvZGluZz4pOg0KICBjb2RpbmcgYw0KICByZXR1cm4gQ29kaW5new0KICAgIHN5c3RlbTogYy5zeXN0ZW0sDQogICAgdmVyc2lvbjogYy52ZXJzaW9uLA0KICAgIGNvZGU6IGMuY29kZSwNCiAgICBkaXNwbGF5OiBjLmRpc3BsYXksDQogICAgdXNlclNlbGVjdGVkOiBjLnVzZXJTZWxlY3RlZA0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBPYnNlcnZhdGlvblZpdGFsU2lnbnNDYXRlZ29yeShjYXRlZ29yeSBMaXN0PENvZGVhYmxlQ29uY2VwdD4pOg0KICBjYXRlZ29yeSBjDQogIHJldHVybiBDb2RlYWJsZUNvbmNlcHR7DQogICAgY29kaW5nOiBPYnNlcnZhdGlvblZpdGFsU2lnbnNDb2RpbmcoYy5jb2RpbmcpLA0KICAgIHRleHQ6IGMudGV4dA0KICB9DQoNCmRlZmluZSBmdW5jdGlvbiBPYnNlcnZhdGlvblZpdGFsU2lnbnNDb21wb25lbnQoY29tcG9uZW50IExpc3Q8RkhJUi5PYnNlcnZhdGlvbi5Db21wb25lbnQ+KToNCiAgY29tcG9uZW50IGMNCiAgcmV0dXJuIEZISVIuT2JzZXJ2YXRpb24uQ29tcG9uZW50ew0KICAgIGNvZGU6IGMuY29kZSwNCiAgICB2YWx1ZTogYy52YWx1ZSwNCiAgICBkYXRhQWJzZW50UmVhc29uOiBjLmRhdGFBYnNlbnRSZWFzb24sDQogICAgaW50ZXJwcmV0YXRpb246IGMuaW50ZXJwcmV0YXRpb24sDQogICAgcmVmZXJlbmNlUmFuZ2U6IFNoYXJlZFJlc291cmNlLk9ic2VydmF0aW9uUmVmZXJlbmNlUmFuZ2UoYy5yZWZlcmVuY2VSYW5nZSkNCiAgfQ0KDQpkZWZpbmUgZnVuY3Rpb24gT2JzZXJ2YXRpb25WaXRhbFNpZ25zUmVzb3VyY2Uob2JzZXJ2YXRpb24gT2JzZXJ2YXRpb24sIHByb2ZpbGVVUkxzIExpc3Q8RkhJUi5jYW5vbmljYWw+KToNCiAgb2JzZXJ2YXRpb24gbw0KICByZXR1cm4gT2JzZXJ2YXRpb257DQogICAgaWQ6IEZISVIuaWQge3ZhbHVlOiAnTENSLScgKyBvLmlkfSwNCiAgICBtZXRhOiBTaGFyZWRSZXNvdXJjZS5NZXRhRWxlbWVudChvLCBwcm9maWxlVVJMcyksDQogICAgZXh0ZW5zaW9uOiBvLmV4dGVuc2lvbiwNCiAgICBwYXJ0T2Y6IG8ucGFydE9mLA0KICAgIHN0YXR1czogby5zdGF0dXMsDQogICAgY2F0ZWdvcnk6IE9ic2VydmF0aW9uVml0YWxTaWduc0NhdGVnb3J5KG8uY2F0ZWdvcnkpLA0KICAgIGNvZGU6IG8uY29kZSwNCiAgICBzdWJqZWN0OiBvLnN1YmplY3QsDQogICAgZm9jdXM6IG8uZm9jdXMsDQogICAgZW5jb3VudGVyOiBvLmVuY291bnRlciwNCiAgICBlZmZlY3RpdmU6IG8uZWZmZWN0aXZlLA0KICAgIGlzc3VlZDogby5pc3N1ZWQsDQogICAgcGVyZm9ybWVyOiBvLnBlcmZvcm1lciwNCiAgICB2YWx1ZTogby52YWx1ZSwNCiAgICBkYXRhQWJzZW50UmVhc29uOiBvLmRhdGFBYnNlbnRSZWFzb24sDQogICAgaW50ZXJwcmV0YXRpb246IG8uaW50ZXJwcmV0YXRpb24sDQogICAgbm90ZTogby5ub3RlLA0KICAgIGJvZHlTaXRlOiBvLmJvZHlTaXRlLA0KICAgIG1ldGhvZDogby5tZXRob2QsDQogICAgc3BlY2ltZW46IG8uc3BlY2ltZW4sDQogICAgZGV2aWNlOiBvLmRldmljZSwNCiAgICByZWZlcmVuY2VSYW5nZTogU2hhcmVkUmVzb3VyY2UuT2JzZXJ2YXRpb25SZWZlcmVuY2VSYW5nZShvLnJlZmVyZW5jZVJhbmdlKSwNCiAgICBoYXNNZW1iZXI6IG8uaGFzTWVtYmVyLA0KICAgIGRlcml2ZWRGcm9tOiBvLmRlcml2ZWRGcm9tLA0KICAgIGNvbXBvbmVudDogT2JzZXJ2YXRpb25WaXRhbFNpZ25zQ29tcG9uZW50KG8uY29tcG9uZW50KQ0KICB9"
			}
		  ]
		},
		"request": {
		  "method": "PUT",
		  "url": "Library/NHSNdQMAcuteCareHospitalInitialPopulation"
		}
	  },
	  {
		"resource": {
		  "resourceType": "ValueSet",
		  "id": "2.16.840.1.113762.1.4.1",
		  "meta": {
			"versionId": "5",
			"lastUpdated": "2015-03-31T01:00:01.000-04:00"
		  },
		  "text": {
			"status": "generated",
			"div": "<div xmlns=\"http://www.w3.org/1999/xhtml\">\n         <h3>Value Set Contents</h3>\n         <p>This value set contains 2 concepts</p>\n         <p>All codes from system \n            <code>http://terminology.hl7.org/CodeSystem/v3-AdministrativeGender</code>\n         </p>\n         <table class=\"codes\">\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <b>Code</b>\n               </td>\n               <td>\n                  <b>Display</b>\n               </td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"http---terminology.hl7.org-CodeSystem-v3-AdministrativeGender-F\"> </a>F\n               </td>\n               <td>Female</td>\n            </tr>\n            <tr>\n               <td style=\"white-space:nowrap\">\n                  <a name=\"http---terminology.hl7.org-CodeSystem-v3-AdministrativeGender-M\"> </a>M\n               </td>\n               <td>Male</td>\n            </tr>\n         </table>\n      </div>"
		  },
		  "url": "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1",
		  "version": "20150331",
		  "name": "ONC Administrative Sex",
		  "title": "ONC Administrative Sex",
		  "status": "active",
		  "date": "2015-03-31T01:00:01-04:00",
		  "publisher": "ONC",
		  "description": "Gender identity restricted to only Male and Female used in administrative situations requiring a restriction to these two categories. ",
		  "expansion": {
			"identifier": "urn:uuid:7c494f5c-d0b0-48b6-9612-99bc4ce6969c",
			"timestamp": "2022-03-09T09:21:02-05:00",
			"total": 2,
			"offset": 0,
			"parameter": [
			  {
				"name": "count",
				"valueInteger": 1000
			  },
			  {
				"name": "offset",
				"valueInteger": 0
			  }
			],
			"contains": [
			  {
				"system": "http://terminology.hl7.org/CodeSystem/v3-AdministrativeGender",
				"version": "20210301",
				"code": "F",
				"display": "Female"
			  },
			  {
				"system": "http://terminology.hl7.org/CodeSystem/v3-AdministrativeGender",
				"version": "20210301",
				"code": "M",
				"display": "Male"
			  }
			]
		  }
		},
		"request": {
		  "method": "PUT",
		  "url": "ValueSet/2.16.840.1.113762.1.4.1/_history/5"
		}
	  }
	]
  },
	"version": {
		"$numberLong": "7"
	},
	"createdDate": new Date(),
	"modifiedDate": null,
	"_class": "com.lantanagroup.link.measureeval.entities.MeasureDefinition"
}	
)

db = connect( 'mongodb://localhost/link-report' );

db.measureReportConfig.insertOne( 
{
  "_id": "6882d4ef-6d3d-404e-8ff9-16c878538404",
  "CreateDate": new Date(),
  "ModifyDate": null,
  "FacilityId": "Test-Hospital",
  "ReportType": "NHSNdQMAcuteCareHospitalInitialPopulation",
  "BundlingType": "Default"
} )