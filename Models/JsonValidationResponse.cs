/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using System.Collections.Generic;
using Newtonsoft.Json.Schema;

namespace ChargePointOperator.Models
{
    public class JsonValidationResponse
        {
        public bool Valid { get; set; }

        public List<ValidationError> Errors { get; set; }

        public string CustomErrors { get; set; }
    }
}
