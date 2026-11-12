using System;
using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Demo
{
    /// <summary>
    /// Turns any parsed document into a <see cref="FactSheet"/>. It implements <see cref="IIwxxmDocumentVisitor{TResult}"/>:
    /// the interface has one <c>Visit</c> overload per concrete type of the closed hierarchy, so if the library adds a
    /// document type this class stops compiling instead of silently printing nothing for it.
    /// </summary>
    internal sealed class DocumentSummaryVisitor : IIwxxmDocumentVisitor<FactSheet>
    {
        private const int MaxListed = 8;

        /// <summary>The common header (type, id, status, usage, issue time) followed by the type-specific facts.</summary>
        public static FactSheet Summarize(IwxxmDocument document)
        {
            var sheet = new FactSheet();
            sheet.Add("Document type", document.ReportType.ToString());
            sheet.Add("Id", document.Id);
            sheet.Add("Status", document.ReportStatus.ToString());
            sheet.Add("Permissible usage", document.PermissibleUsage.ToString());
            sheet.Add("Usage reason", document.PermissibleUsageReason?.ToString());
            sheet.Add("Issue time (UTC)", Fmt.Time(document.IssueTimeUtc));

            if (document is IwxxmReport report)
            {
                if (report.PermissibleUsageSupplementary != null)
                {
                    sheet.Add("Usage note", report.PermissibleUsageSupplementary);
                }

                if (report.IsTranslationFailed)
                {
                    sheet.Add("Translation failed", "yes, original TAC: " + Fmt.Or(System.Text.RegularExpressions.Regex.Replace(report.TranslationFailedTac ?? string.Empty, @"\s+", " ").Trim()));
                }

                if (report.Translation != null)
                {
                    TranslationMetadata t = report.Translation;
                    sheet.Add("Translated by", $"{Fmt.Or(t.TranslationCentreDesignator)} ({Fmt.Or(t.TranslationCentreName)}) at {Fmt.Time(t.TranslationTimeUtc)}");
                }
            }

            sheet.AppendFlat(document.Accept(new DocumentSummaryVisitor()));
            return sheet;
        }

        // ---- METAR / SPECI ------------------------------------------------------------------------------------

        public FactSheet Visit(Metar metar) => Observation(metar);

        public FactSheet Visit(Speci speci) => Observation(speci);

        private static FactSheet Observation(AerodromeObservation obs)
        {
            var s = new FactSheet();
            s.Add("Aerodrome", $"{Fmt.Or(obs.AerodromeDesignator)} ({Fmt.Or(obs.AerodromeName)})");
            s.Add("Aerodrome position", Fmt.Text(obs.AerodromePosition));
            s.Add("Observation time (UTC)", Fmt.Time(obs.ObservationTimeUtc));
            s.Add("Automated station", Fmt.Yes(obs.IsAutomatedStation));

            if (obs.IsNilObservation)
            {
                s.Add("NIL observation", "yes, reason: " + Fmt.Or(obs.ObservationNilReason));
                return s;
            }

            s.Add("CAVOK", Fmt.Yes(obs.CloudAndVisibilityOk));
            s.Add("Air temperature", Fmt.Number(obs.AirTemperatureCelsius, " C"));
            s.Add("Dew point", Fmt.Number(obs.DewpointTemperatureCelsius, " C"));
            s.Add("QNH", Fmt.Number(obs.QnhHpa, " hPa"));
            AddWind(s, obs.SurfaceWind);

            AerodromeHorizontalVisibility? vis = obs.Visibility;
            s.Add("Visibility", Fmt.WithOperator(vis?.PrevailingOperator, vis?.PrevailingMeters, " m"));
            if (vis?.MinimumMeters != null)
            {
                s.Add("Minimum visibility", $"{Fmt.Number(vis.MinimumMeters, " m")} towards {Fmt.Number(vis.MinimumDirectionDegrees, " deg")}");
            }

            if (obs.RunwayVisualRanges.Count > 0)
            {
                s.Heading($"Runway visual range ({obs.RunwayVisualRanges.Count})");
                s.Nest(() =>
                {
                    foreach (RunwayVisualRange rvr in obs.RunwayVisualRanges)
                    {
                        s.Add("Runway " + Fmt.Or(rvr.RunwayDesignator), $"{Fmt.WithOperator(rvr.MeanRvrOperator, rvr.MeanRvrMeters, " m")}, tendency {Fmt.Text(rvr.PastTendency)}");
                    }
                });
            }

            AddWeather(s, obs.PresentWeather, obs.PresentWeatherNilReason);
            AddClouds(s, obs.Cloud);

            if (obs.TrendForecasts.Count > 0)
            {
                s.Heading($"Trend forecasts ({obs.TrendForecasts.Count})");
                s.Nest(() =>
                {
                    foreach (AerodromeForecastGroup trend in obs.TrendForecasts)
                    {
                        AddForecastGroup(s, trend, $"Trend {Fmt.Or(trend.ChangeIndicator)}");
                    }
                });
            }

            return s;
        }

        // ---- TAF ----------------------------------------------------------------------------------------------

        public FactSheet Visit(Taf taf)
        {
            var s = new FactSheet();
            s.Add("Aerodrome", $"{Fmt.Or(taf.AerodromeDesignator)} ({Fmt.Or(taf.AerodromeName)})");
            s.Add("Aerodrome position", Fmt.Text(taf.AerodromePosition));
            s.Add("Valid (UTC)", Fmt.Period(taf.ValidFromUtc, taf.ValidUntilUtc));

            if (taf.IsCancelReport)
            {
                s.Add("Cancellation", "cancels the TAF valid " + Fmt.Period(taf.CancelledReportValidFromUtc, taf.CancelledReportValidUntilUtc));
                return s;
            }

            if (taf.IsNilForecast)
            {
                s.Add("NIL forecast", "yes, reason: " + Fmt.Or(taf.BaseForecastNilReason));
                return s;
            }

            if (taf.BaseForecast != null)
            {
                s.Heading("Base forecast");
                s.Nest(() => AddForecastGroup(s, taf.BaseForecast, null));
            }

            s.Heading($"Change groups ({taf.ChangeForecasts.Count})");
            s.Nest(() =>
            {
                foreach (AerodromeForecastGroup change in taf.ChangeForecasts)
                {
                    AddForecastGroup(s, change, Fmt.Or(change.ChangeIndicator));
                }
            });
            return s;
        }

        private static void AddForecastGroup(FactSheet s, AerodromeForecastGroup g, string? title)
        {
            if (title != null)
            {
                s.Heading(title);
                s.Nest(() => AddForecastGroup(s, g, null));
                return;
            }

            s.Add("Trend time indicator", Fmt.Text(g.TimeIndicator));
            s.Add("Valid (UTC)", Fmt.Period(g.ValidFromUtc, g.ValidUntilUtc));
            s.Add("CAVOK", Fmt.Yes(g.CloudAndVisibilityOk));
            s.Add("Visibility", Fmt.WithOperator(g.PrevailingVisibilityOperator, g.PrevailingVisibilityMeters, " m"));
            AddWind(s, g.SurfaceWind);
            AddWeather(s, g.Weather, g.WeatherNilReason);
            AddClouds(s, g.Cloud);
            foreach (AerodromeTemperatureForecast t in g.Temperatures)
            {
                s.Add("Temperature", $"max {Fmt.Number(t.MaximumCelsius, " C")} at {Fmt.Time(t.MaximumTimeUtc)}, min {Fmt.Number(t.MinimumCelsius, " C")} at {Fmt.Time(t.MinimumTimeUtc)}");
            }
        }

        // ---- shared aerodrome helpers -------------------------------------------------------------------------

        private static void AddWind(FactSheet s, SurfaceWind? wind)
        {
            if (wind == null)
            {
                s.Add("Surface wind", null);
                return;
            }

            string direction = wind.IsVariableDirection ? "variable" : Fmt.Number(wind.MeanDirectionDegrees, " deg");
            s.Add("Surface wind", $"{direction} at {Fmt.SpeedText(wind.MeanSpeed)}" +
                (wind.GustSpeed != null ? $", gusting {Fmt.SpeedText(wind.GustSpeed)}" : string.Empty));
            if (wind.ExtremeClockwiseDirectionDegrees != null || wind.ExtremeCounterClockwiseDirectionDegrees != null)
            {
                s.Add("Wind direction range", $"{Fmt.Number(wind.ExtremeCounterClockwiseDirectionDegrees, " deg")} to {Fmt.Number(wind.ExtremeClockwiseDirectionDegrees, " deg")}");
            }
        }

        private static void AddWeather(FactSheet s, IReadOnlyList<AerodromeWeather> weather, CodeListValue<NilReason>? nilReason)
        {
            string text = weather.Count > 0
                ? string.Join(", ", weather.Select(w => w.IsKnown ? w.Notation : w.Notation + " (not in the official code list)"))
                : nilReason != null ? "none, " + nilReason : Fmt.Dash;
            s.Add("Present weather", text);
        }

        private static void AddClouds(FactSheet s, AerodromeCloud? cloud)
        {
            if (cloud == null)
            {
                s.Add("Clouds", null);
                return;
            }

            if (cloud.NilReason != null)
            {
                s.Add("Clouds", "none, " + cloud.NilReason);
            }
            else
            {
                s.Heading($"Clouds ({cloud.Layers.Count} layer(s))");
            }

            s.Nest(() =>
            {
                foreach (AerodromeCloudLayer layer in cloud.Layers)
                {
                    string baseText = layer.Base != null ? Fmt.Text(layer.Base) : layer.BaseNilReason != null ? Fmt.Text(layer.BaseNilReason) : Fmt.Dash;
                    s.Add("Layer", $"{Fmt.Text(layer.Amount ?? (object?)layer.AmountNilReason)}, base {baseText}, type {Fmt.Text(layer.CloudType)}");
                }

                if (cloud.VerticalVisibility != null || cloud.VerticalVisibilityNilReason != null)
                {
                    s.Add("Vertical visibility", cloud.VerticalVisibility != null ? Fmt.Text(cloud.VerticalVisibility) : Fmt.Text(cloud.VerticalVisibilityNilReason));
                }
            });
        }

        // ---- en-route (SIGMET / AIRMET) ----------------------------------------------------------------------

        public FactSheet Visit(Sigmet sigmet)
        {
            var s = new FactSheet();
            s.Add("FIR", $"{Fmt.Or(sigmet.FirDesignator)} ({Fmt.Or(sigmet.FirName)})");
            s.Add("ATS unit", $"{Fmt.Or(sigmet.AtsUnitDesignator)} ({Fmt.Or(sigmet.AtsUnitName)})");
            s.Add("MWO", sigmet.MwoDesignator);
            s.Add("Sequence number", sigmet.SequenceNumber);
            s.Add("Valid (UTC)", Fmt.Period(sigmet.ValidFromUtc, sigmet.ValidUntilUtc));
            s.Add("Phenomenon", Fmt.Text(sigmet.Phenomenon));

            if (sigmet.IsCancelReport)
            {
                s.Add("Cancellation", $"cancels SIGMET {Fmt.Or(sigmet.CancelledReportSequenceNumber)} valid {Fmt.Period(sigmet.CancelledReportValidFromUtc, sigmet.CancelledReportValidUntilUtc)}");
            }

            if (sigmet.EruptingVolcano != null)
            {
                s.Add("Erupting volcano", VolcanoText(sigmet.EruptingVolcano));
            }

            if (sigmet.TropicalCycloneName != null)
            {
                s.Add("Tropical cyclone", sigmet.TropicalCycloneName);
            }

            if (sigmet.VolcanicAshMovedToFirDesignator != null)
            {
                s.Add("Ash moved to FIR", $"{sigmet.VolcanicAshMovedToFirDesignator} ({Fmt.Or(sigmet.VolcanicAshMovedToFirName)})");
            }

            AddVolumes(s, "Evolving conditions", sigmet.EvolvingConditions);
            AddVolumes(s, "Forecast positions", sigmet.ForecastPositions);
            AddVolumes(s, "Supplementary evolving conditions", sigmet.SupplementaryEvolvingConditions);
            AddVolumes(s, "Supplementary forecast positions", sigmet.SupplementaryForecastPositions);
            return s;
        }

        public FactSheet Visit(Airmet airmet)
        {
            var s = new FactSheet();
            s.Add("FIR", $"{Fmt.Or(airmet.FirDesignator)} ({Fmt.Or(airmet.FirName)})");
            s.Add("ATS unit", $"{Fmt.Or(airmet.AtsUnitDesignator)} ({Fmt.Or(airmet.AtsUnitName)})");
            s.Add("MWO", airmet.MwoDesignator);
            s.Add("Sequence number", airmet.SequenceNumber);
            s.Add("Valid (UTC)", Fmt.Period(airmet.ValidFromUtc, airmet.ValidUntilUtc));
            s.Add("Phenomenon", Fmt.Text(airmet.Phenomenon));

            if (airmet.IsCancelReport)
            {
                s.Add("Cancellation", $"cancels AIRMET {Fmt.Or(airmet.CancelledReportSequenceNumber)} valid {Fmt.Period(airmet.CancelledReportValidFromUtc, airmet.CancelledReportValidUntilUtc)}");
            }

            if (airmet.EvolvingConditions.Count > 0)
            {
                s.Heading($"Evolving conditions ({airmet.EvolvingConditions.Count})");
                s.Nest(() =>
                {
                    foreach (AirmetEvolvingCondition condition in airmet.EvolvingConditions)
                    {
                        AddVolume(s, condition.Volume);
                        s.Nest(() =>
                        {
                            s.Add("Cloud base / top", $"{Fmt.Text(condition.CloudBase)} / {Fmt.Text(condition.CloudTop)}");
                            s.Add("Surface visibility", Fmt.Text(condition.SurfaceVisibility));
                            s.Add("Visibility reduced by", Fmt.List(condition.SurfaceVisibilityCauses.Select(c => c.ToString())));
                            s.Add("Surface wind", $"{Fmt.Number(condition.SurfaceWindDirectionDegrees, " deg")} at {Fmt.SpeedText(condition.SurfaceWindSpeed)}");
                        });
                    }
                });
            }

            return s;
        }

        private static void AddVolumes(FactSheet s, string title, IReadOnlyList<EnRoutePhenomenonVolume> volumes)
        {
            if (volumes.Count == 0)
            {
                return;
            }

            s.Heading($"{title} ({volumes.Count})");
            s.Nest(() =>
            {
                foreach (EnRoutePhenomenonVolume volume in volumes.Take(MaxListed))
                {
                    AddVolume(s, volume);
                }

                AddMore(s, volumes.Count);
            });
        }

        private static void AddVolume(FactSheet s, EnRoutePhenomenonVolume v)
        {
            s.Heading($"{Fmt.Text(v.TimeIndicator)} at {Fmt.Time(v.PhenomenonTimeUtc)}");
            s.Nest(() =>
            {
                s.Add("Area", Fmt.Surface(v.Area));
                s.Add("Levels", Fmt.Levels(v.LowerLimit, v.UpperLimit));
                s.Add("Intensity change", v.IntensityChange);
                if (v.DirectionOfMotionDegrees != null || v.SpeedOfMotion != null)
                {
                    s.Add("Motion", $"{Fmt.Number(v.DirectionOfMotionDegrees, " deg")} at {Fmt.SpeedText(v.SpeedOfMotion)}");
                }

                if (v.TropicalCycloneCentre != null)
                {
                    s.Add("Cyclone centre", Fmt.Text(v.TropicalCycloneCentre));
                }
            });
        }

        private static void AddMore(FactSheet s, int total)
        {
            if (total > MaxListed)
            {
                s.Add("...", $"{total - MaxListed} more not shown");
            }
        }

        private static string VolcanoText(Volcano v) =>
            $"{Fmt.Or(v.Name)} (IAVCEI {Fmt.Or(v.IavceiNumber)}) at {v.Position}, elevation {Fmt.Text(v.SourceElevation)}, status {Fmt.Text(v.ActivityStatus)}";

        // ---- volcano family -----------------------------------------------------------------------------------

        public FactSheet Visit(Vaa vaa)
        {
            var s = new FactSheet();
            s.Add("VAAC", vaa.IssuingVaacName);
            s.Add("Advisory number", vaa.AdvisoryNumber);
            s.Add("Volcano", vaa.Volcano != null ? VolcanoText(vaa.Volcano) : null);
            s.Add("State or region", vaa.StateOrRegion);
            s.Add("Source elevation", Fmt.Text(vaa.SourceElevation));
            s.Add("Information source", vaa.InformationSource);
            s.Add("Eruption details", vaa.EruptionDetails);
            s.Add("Remarks", vaa.Remarks);
            s.Add("Next advisory (UTC)", Fmt.Time(vaa.NextAdvisoryTimeUtc));

            if (vaa.Observation != null)
            {
                AddAshConditions(s, "Observation", vaa.Observation);
            }

            foreach (VolcanicAshConditions forecast in vaa.Forecasts)
            {
                AddAshConditions(s, "Forecast", forecast);
            }

            return s;
        }

        private static void AddAshConditions(FactSheet s, string title, VolcanicAshConditions conditions)
        {
            s.Heading($"{title} at {Fmt.Time(conditions.PhenomenonTimeUtc)}: {Fmt.Text(conditions.Status)}, {conditions.AshClouds.Count} ash cloud(s)");
            s.Nest(() =>
            {
                foreach (EnRoutePhenomenonVolume cloud in conditions.AshClouds.Take(MaxListed))
                {
                    AddVolume(s, cloud);
                }

                AddMore(s, conditions.AshClouds.Count);
            });
        }

        public FactSheet Visit(Vona vona)
        {
            var s = new FactSheet();
            s.Add("Originating centre", $"{vona.OriginatingCentre} ({Fmt.Or(vona.OriginatingCentreDesignator)})");
            s.Add("State or region", vona.StateOrRegion);
            s.Add("Notice number", vona.NoticeNumber);
            s.Add("Colour code", $"{Fmt.Text(vona.CurrentColourCode)} (previous {Fmt.Text(vona.PreviousColourCode)})");
            foreach (Volcano volcano in vona.Volcanoes)
            {
                s.Add("Volcano", VolcanoText(volcano));
            }

            s.Add("Ash cloud elevation", Fmt.Text(vona.AshCloudElevation));
            s.Add("Contacts", vona.Contacts);
            s.Add("Remarks", vona.Remarks);
            s.Add("Next notice", vona.NextNotice);
            return s;
        }

        public FactSheet Visit(Qvaci qvaci)
        {
            var s = new FactSheet();
            s.Add("Originating centre", $"{qvaci.OriginatingCentre} ({Fmt.Or(qvaci.OriginatingCentreDesignator)})");
            s.Add("Model initialised (UTC)", Fmt.Time(qvaci.DispersionModelInitialisationTimeUtc));
            s.Add("Remarks", qvaci.Remarks);
            foreach (Volcano volcano in qvaci.Volcanoes)
            {
                s.Add("Volcano", VolcanoText(volcano));
            }

            s.Heading($"Ash concentration contours ({qvaci.AshConcentrationClouds.Count})");
            s.Nest(() =>
            {
                foreach (VolcanicAshConcentrationCloud cloud in qvaci.AshConcentrationClouds.Take(MaxListed))
                {
                    s.Add("Contour", $"{Fmt.Number(cloud.AshDensityMgPerM3, " mg/m3")}, levels {Fmt.Levels(cloud.LowerLimit, cloud.UpperLimit)}, at {Fmt.Time(cloud.PhenomenonTimeUtc)}");
                    s.Nest(() => s.Add("Area", Fmt.Polygon(cloud.Area)));
                }

                AddMore(s, qvaci.AshConcentrationClouds.Count);
            });
            return s;
        }

        // ---- tropical cyclone / space weather -----------------------------------------------------------------

        public FactSheet Visit(Tca tca)
        {
            var s = new FactSheet();
            s.Add("TCAC", tca.IssuingTcacDesignator);
            s.Add("Cyclone", tca.CycloneName);
            s.Add("Advisory number", tca.AdvisoryNumber);
            s.Add("Remarks", tca.Remarks);
            s.Add("Next advisory (UTC)", Fmt.Time(tca.NextAdvisoryTimeUtc));

            if (tca.Observation != null)
            {
                s.Add("Observed", TrackPointText(tca.Observation));
            }

            s.Heading($"Forecast track points ({tca.Forecasts.Count})");
            s.Nest(() =>
            {
                foreach (TropicalCycloneTrackPoint point in tca.Forecasts.Take(MaxListed))
                {
                    s.Add("Forecast", TrackPointText(point));
                }

                AddMore(s, tca.Forecasts.Count);
            });
            AddVolumes(s, "Cumulonimbus locations", tca.CumulonimbusCloudLocations);
            return s;
        }

        private static string TrackPointText(TropicalCycloneTrackPoint p) =>
            $"{Fmt.Time(p.PhenomenonTimeUtc)} at {p.Position}, max wind {Fmt.SpeedText(p.MaxSurfaceWindSpeed)}, " +
            $"pressure {Fmt.Number(p.CentralPressureHpa, " hPa")}, movement {Fmt.Text(p.Movement)}, {Fmt.Number(p.MovementDirectionDegrees, " deg")} at {Fmt.SpeedText(p.MovementSpeed)}";

        public FactSheet Visit(SpaceWx swx)
        {
            var s = new FactSheet();
            s.Add("SWXC", swx.IssuingSwxcName);
            s.Add("Advisory number", swx.AdvisoryNumber);
            s.Add("Replaces", Fmt.List(swx.ReplacedAdvisoryNumbers));
            s.Add("Effects", Fmt.List(swx.Effects));
            s.Add("Remarks", swx.Remarks);
            s.Add("Next advisory (UTC)", Fmt.Time(swx.NextAdvisoryTimeUtc));

            s.Heading($"Analyses ({swx.Analyses.Count})");
            s.Nest(() =>
            {
                foreach (SpaceWeatherAnalysis analysis in swx.Analyses)
                {
                    s.Heading($"{Fmt.Text(analysis.TimeIndicator)} at {Fmt.Time(analysis.PhenomenonTimeUtc)}" +
                        (analysis.IsNothingOfOperationalSignificance ? ": no space weather expected" : string.Empty));
                    s.Nest(() =>
                    {
                        foreach (SpaceWeatherAnalysisRegion region in analysis.Regions)
                        {
                            s.Add("Region", $"{Fmt.Or(region.Intensity)}, locations [{Fmt.List(region.LocationIndicators.Select(l => l.ToString()))}]");
                            s.Nest(() =>
                            {
                                foreach (GeoSurface area in region.Areas)
                                {
                                    s.Add("Area", Fmt.Surface(area));
                                }
                            });
                        }
                    });
                }
            });
            return s;
        }

        // ---- SIGWX --------------------------------------------------------------------------------------------

        public FactSheet Visit(Sigwx sigwx)
        {
            var s = new FactSheet();
            s.Add("Issuing centre", sigwx.IssuingCentre);
            s.Add("Valid (UTC)", Fmt.Period(sigwx.ValidFromUtc, sigwx.ValidUntilUtc));
            s.Add("Vertical envelope", Fmt.Levels(sigwx.LowerLimit, sigwx.UpperLimit));

            s.Heading("Features");
            s.Nest(() =>
            {
                s.Add("Jet streams", $"{sigwx.JetStreams.Count} ({sigwx.JetStreams.Sum(j => j.AxisTrajectory.Count)} axis points, {sigwx.JetStreams.Sum(j => j.CorePoints.Count)} core points)");
                s.Add("Clouds", sigwx.Clouds.Count.ToString());
                s.Add("Turbulence areas", sigwx.Turbulences.Count.ToString());
                s.Add("Icing areas", sigwx.AirframeIcings.Count.ToString());
                s.Add("Tropopause", $"{sigwx.Tropopauses.Count} ({sigwx.Tropopauses.Count(t => t.ContourPolygon != null)} contours, {sigwx.Tropopauses.Sum(t => t.SpotHeights.Count)} spot heights)");
                s.Add("Volcanoes", sigwx.Volcanoes.Count.ToString());
                s.Add("Tropical cyclones", sigwx.TropicalCyclones.Count.ToString());
                s.Add("Sandstorms", sigwx.Sandstorms.Count.ToString());
                s.Add("Radiation incidents", sigwx.RadiationIncidents.Count.ToString());
                s.Add("Features not mapped", sigwx.FeatureErrors.Count.ToString());
            });

            if (sigwx.JetStreams.Count > 0)
            {
                s.Heading("Jet streams");
                s.Nest(() =>
                {
                    foreach (JetStream jet in sigwx.JetStreams.Take(MaxListed))
                    {
                        s.Add(jet.Id, $"max {Fmt.SpeedText(jet.MaxWindSpeed)} at {Fmt.Text(jet.CoreLevel)}, {jet.AxisTrajectory.Count} axis points");
                    }

                    AddMore(s, sigwx.JetStreams.Count);
                });
            }

            AddAreaFeatures(s, "Clouds", sigwx.Clouds, c => $"{Fmt.Text(c.Distribution)} {Fmt.Text(c.CloudType)}, {Fmt.Levels(c.Base, c.Top)}", c => c.Areas);
            AddAreaFeatures(s, "Turbulence", sigwx.Turbulences, t => $"{Fmt.Text(t.Degree)}, {Fmt.Levels(t.Base, t.Top)}", t => t.Areas);
            AddAreaFeatures(s, "Icing", sigwx.AirframeIcings, i => $"{Fmt.Text(i.Degree)}, {Fmt.Levels(i.Base, i.Top)}", i => i.Areas);

            if (sigwx.Volcanoes.Count + sigwx.TropicalCyclones.Count > 0)
            {
                s.Heading("Point features");
                s.Nest(() =>
                {
                    foreach (Volcano volcano in sigwx.Volcanoes.Take(MaxListed))
                    {
                        s.Add("Volcano", VolcanoText(volcano));
                    }

                    foreach (TropicalCyclone tc in sigwx.TropicalCyclones.Take(MaxListed))
                    {
                        s.Add("Tropical cyclone", $"{tc.Name} at {tc.Position}");
                    }
                });
            }

            if (sigwx.FeatureErrors.Count > 0)
            {
                s.Heading("Skipped features");
                s.Nest(() =>
                {
                    foreach (ItemMappingError skipped in sigwx.FeatureErrors.Take(MaxListed))
                    {
                        s.Add($"Feature #{skipped.Index}", Fmt.Or(skipped.Errors.FirstOrDefault()?.Code));
                    }
                });
            }

            return s;
        }

        private static void AddAreaFeatures<T>(FactSheet s, string title, IReadOnlyList<T> features, Func<T, string> describe, Func<T, IReadOnlyList<GeoSurface>> areas)
        {
            if (features.Count == 0)
            {
                return;
            }

            s.Heading(title);
            s.Nest(() =>
            {
                foreach (T feature in features.Take(3))
                {
                    s.Add("Feature", describe(feature));
                    s.Nest(() =>
                    {
                        IReadOnlyList<GeoSurface> surfaces = areas(feature);
                        s.Add("Geometry", surfaces.Count == 1 ? Fmt.Surface(surfaces[0]) : $"{surfaces.Count} surfaces");
                    });
                }

                AddMore(s, features.Count);
            });
        }

        // ---- bulletin -----------------------------------------------------------------------------------------

        public FactSheet Visit(MeteorologicalBulletin bulletin)
        {
            var s = new FactSheet();
            s.Add("Bulletin identifier", bulletin.BulletinIdentifier);
            s.Add("Entries", $"{bulletin.ReportResults.Count} ({bulletin.Reports.Count} mapped, {bulletin.ItemErrors.Count} skipped)");

            int number = 0;
            foreach (IwxxmReport report in bulletin.Reports)
            {
                number++;
                s.Heading($"Report {number} of {bulletin.Reports.Count}: {report.ReportType}");

                // Nested visiting: the same entry point, applied to each contained report.
                s.Append(Summarize(report));
            }

            foreach (ItemMappingError skipped in bulletin.ItemErrors)
            {
                s.Heading($"Skipped entry #{skipped.Index} (id {Fmt.Or(skipped.ItemId)})");
                s.Nest(() =>
                {
                    foreach (ValidationError error in skipped.Errors)
                    {
                        s.Add(error.Severity.ToString(), $"{error.Code} {error.Message}");
                    }
                });
            }

            return s;
        }
    }
}
