﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using ICanHasDotnetCore.Investigator;
using ICanHasDotnetCore.NugetPackages;
using ICanHasDotnetCore.SourcePackageFileReaders;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Linq;
using ICanHasDotnetCore.Plumbing.Extensions;

namespace ICanHasDotnetCore.Web.Features.Statistics
{
    public class RequerySupportTypeForStatisticsPackagesTask : IStartable, IDisposable
    {
        private readonly IStatisticsRepository _statisticsRepository;
        private Timer _timer;

        public RequerySupportTypeForStatisticsPackagesTask(IStatisticsRepository statisticsRepository)
        {
            _statisticsRepository = statisticsRepository;
        }

        public void Start()
        {
            Log.Information("Starting Requery Statistics Package Support Task timer");
            _timer = new Timer(_ => Run().Wait(), null, TimeSpan.FromMinutes(10), TimeSpan.FromDays(1));
        }


        public void Dispose()
        {
            _timer?.Dispose();
        }

        public async Task Run()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                Log.Information("Requery Statistics Package Support Task Started");
                var stats = _statisticsRepository.GetAllPackageStatistics();
                var packageNames = stats.Select(s => s.Name).ToArray();

                var result = await PackageCompatabilityInvestigator.Create(new NoNugetResultCache())
                    .Process("Requery", packageNames);

                foreach (var stat in stats)
                {
                    var packageResult = result.Dependencies.FirstOrDefault(f => f.PackageName.EqualsOrdinalIgnoreCase(stat.Name));
                    UpdatePackage(packageResult, stat);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Requery Statistics Package Support Task failed");
            }
            Log.Information("Requery Statistics Package Support Task Finished in {time}", sw.Elapsed);
        }

        private void UpdatePackage(PackageResult packageResult, PackageStatistic stat)
        {
            if (packageResult == null)
            {
                Log.Information("No result returned for {package}", stat.Name);
            }
            else if (!packageResult.WasSuccessful)
            {
                Log.Information("Error occured for retrieving {package}: {error}", stat.Name, packageResult.Error);
            }
            else if (stat.LatestSupportType != packageResult.SupportType && packageResult.SupportType != SupportType.Error)
            {
                Log.Information("Updating support type for {package} from {from} to {to}", stat.Name, stat.LatestSupportType,
                    packageResult.SupportType);
                _statisticsRepository.UpdateSupportTypeFor(stat, packageResult.SupportType);
            }
        }

    }
}