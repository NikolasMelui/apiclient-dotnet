using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Voximplant.API.Response {

    public class UpdateSipRegistrationResponse : BaseResponse
    {
        /// <summary>
        /// 1
        /// </summary>
        [JsonProperty("result")]
        public long Result { get; private set; }

    }
}