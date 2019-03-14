﻿//-----------------------------------------------------------------------------
// FILE:	    Test_CodeGen
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.CodeGen;
using Neon.Common;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestCodeGen.CodeGen
{
    public interface EmptyData
    {
    }

    public enum MyEnum1
    {
        One,
        Two,
        Three
    }

    [Flags]
    public enum MyEnum2 : int
    {
        [EnumMember(Value = "one")]
        One = 1,
        [EnumMember(Value = "two")]
        Two = 2,
        [EnumMember(Value = "three")]
        Three = 3
    }

    public interface SimpleData
    {
        string Name { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(5)]
        int Age { get; set; }
    }

    public interface BasicTypes
    {
        bool Bool { get; set; }
        byte Byte { get; set; }
        sbyte SByte { get; set; }
        short Short { get; set; }
        ushort UShort { get; set; }
        int Int { get; set; }
        uint UInt { get; set; }
        long Long { get; set; }
        ulong ULong { get; set; }
        float Float { get; set; }
        double Double { get; set; }
        decimal Decimal { get; set; }
        string String { get; set; }
    }

    public interface ComplexData
    {
        List<string> Items { get; set; }
        Dictionary<string, int> Lookup { get; set; }
        MyEnum1 Enum1 { get; set; }
        MyEnum2 Enum2 { get; set; }
        SimpleData Simple { get; set; }
        int[] SingleArray { get; set; }
        int[][] DoubleArray { get; set; }
        int[][][] TripleArray { get; set; }

        [JsonIgnore]
        int IgnoreThis { get; set; }
    }

    public interface NoSetter
    {
        string Value { get; }
    }

    public interface ParentModel
    {
        string ParentProperty { get; set; }
    }

    public interface ChildModel : ParentModel
    {
        string ChildProperty { get; set; }
    }

    [NoCodeGen]
    public class Test_CodeGen
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void DataModel_Empty()
        {
            // Verify that we can generate code for an empty data model.

            var settings = new CodeGeneratorSettings()
            {
                 SourceNamespace = typeof(Test_CodeGen).Namespace,
                 ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<EmptyData>();

                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));

                data = context.CreateDataWrapperFrom<EmptyData>("{}");

                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));

                data = context.CreateDataWrapperFrom<EmptyData>(new JObject());

                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));
            }
        }
    }
}
