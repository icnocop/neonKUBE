﻿//-----------------------------------------------------------------------------
// FILE:	    RoundTripJsonOutputFormatter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

using Neon.Common;
using Neon.Data;

namespace Neon.Web
{
    /// <summary>
    /// <para>
    /// Handles serialization of JSON objects for noSQL scenarios that supports round 
    /// trips without any property loss, even if one side of the transaction is out 
    /// of data and is not aware of all of the possible JSON properties.
    /// </para>
    /// <para>
    /// This class is designed to support classes generated by the <b>Neon.CodeGen</b>
    /// assembly that implement <see cref=" IGeneratedType"/>.
    /// </para>
    /// </summary>
    public sealed class RoundTripJsonOutputFormatter : TextOutputFormatter
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public RoundTripJsonOutputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/json"));
            SupportedEncodings.Add(Encoding.UTF8);
        }

        /// <inheritdoc/>
        protected override bool CanWriteType(Type type)
        {
            if (WebHelper.IsRoundTripType(type))
            {
                return true;
            }

            if (type.Implements<IGeneratedType>())
            {
                WebHelper.RegisterRoundTripType(type);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            var response = context.HttpContext.Response;

            if (context.Object == null)
            {
                await response.WriteAsync("null");
            }
            else
            {
                var generated = context.Object as IGeneratedType;

                if (generated != null)
                {
                    await generated.WriteJsonToAsync(response.Body);
                }
                else
                {
                    await response.WriteAsync(NeonHelper.JsonSerialize(context.Object));
                }
            }
        }
    }
}
