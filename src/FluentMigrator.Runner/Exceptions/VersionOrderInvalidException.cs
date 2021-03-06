#region License
//
// Copyright (c) 2018, Fluent Migrator Project
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System;
using System.Collections.Generic;

using FluentMigrator.Infrastructure;

namespace FluentMigrator.Runner.Exceptions
{
    public class VersionOrderInvalidException : RunnerException
    {
        public IEnumerable<KeyValuePair<long, IMigrationInfo>> InvalidMigrations { get; set; }

        public IEnumerable<long> InvalidVersions { get; private set; }

        public VersionOrderInvalidException(IEnumerable<KeyValuePair<long, IMigrationInfo>> invalidMigrations)
        {
            InvalidMigrations = invalidMigrations;
        }

        public override string Message
        {
            get
            {
                var result = "Unapplied migrations have version numbers that are less than the greatest version number of applied migrations:";

                foreach (var pair in InvalidMigrations)
                {
                    result = result + string.Format("{0}{1} - {2}", Environment.NewLine, pair.Key, pair.Value.Migration.GetType().Name);
                }

                return result;
            }
        }
    }
}
