<?xml version="1.0" encoding="UTF-8"?>
<!--
  Precompiled ISO Schematron XSLT 1.0 stylesheet for WMO/ICAO IWXXM 2025-2 SIGWX reports.
  Enforces business rules from official WMO rule/iwxxm.sch and ICAO Annex 3 for both
  <iwxxm:WAFSSignificantWeatherForecast> and <iwxxm:SIGWXReport>.
  Produces standard SVRL (Schematron Validation Report Language) output compatible with
  System.Xml.Xsl.XslCompiledTransform in .NET Standard 2.0.
-->
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:svrl="http://purl.oclc.org/dsdl/svrl"
                xmlns:iwxxm="http://icao.int/iwxxm/2025-2"
                xmlns:gml="http://www.opengis.net/gml/3.2"
                xmlns:xlink="http://www.w3.org/1999/xlink"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

  <xsl:output method="xml" indent="yes" encoding="UTF-8"/>

  <xsl:template match="/">
    <svrl:schematron-output title="IWXXM 2025-2 SIGWX Business Rules Validation" schemaVersion="2025-2">
      <xsl:apply-templates select="iwxxm:WAFSSignificantWeatherForecast | iwxxm:SIGWXReport"/>
    </svrl:schematron-output>
  </xsl:template>

  <!-- =========================================================================
       Official WMO IWXXM 2025-2 WAFSSignificantWeatherForecast Business Rules
       ========================================================================= -->
  <xsl:template match="iwxxm:WAFSSignificantWeatherForecast">
    <svrl:active-pattern id="WAFSSignificantWeatherForecast-Rules" name="WMO IWXXM 2025-2 WAFS SIGWX Rules"/>

    <!-- Rule: MeteorologicalFeature.MeteorologicalFeatureCollection-1 -->
    <xsl:if test="not(gml:identifier)">
      <svrl:failed-assert id="MeteorologicalFeature.MeteorologicalFeatureCollection-1"
                          role="ERROR"
                          location="/iwxxm:WAFSSignificantWeatherForecast"
                          test="exists(gml:identifier)">
        <svrl:text>MeteorologicalFeature.MeteorologicalFeatureCollection-1: gml:identifier is mandatory</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <!-- Rule: WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-1 -->
    <xsl:if test="string(iwxxm:phenomenonCategory) != 'weatherForecasts'">
      <svrl:failed-assert id="WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-1"
                          role="ERROR"
                          location="/iwxxm:WAFSSignificantWeatherForecast/iwxxm:phenomenonCategory"
                          test="iwxxm:phenomenonCategory = 'weatherForecasts'">
        <svrl:text>WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-1: iwxxm:phenomenonCategory shall be equal to 'weatherForecasts'.</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <!-- Rule: WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-2 -->
    <xsl:if test="not(iwxxm:phenomenonBaseTime)">
      <svrl:failed-assert id="WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-2"
                          role="ERROR"
                          location="/iwxxm:WAFSSignificantWeatherForecast"
                          test="exists(iwxxm:phenomenonBaseTime)">
        <svrl:text>WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-2: iwxxm:phenomenonBaseTime is mandatory</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <!-- Rule: WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-3 -->
    <xsl:if test="not(iwxxm:originatingCentre/iwxxm:WorldAreaForecastCentre)">
      <svrl:failed-assert id="WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-3"
                          role="ERROR"
                          location="/iwxxm:WAFSSignificantWeatherForecast/iwxxm:originatingCentre"
                          test="exists(iwxxm:originatingCentre/iwxxm:WorldAreaForecastCentre)">
        <svrl:text>WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-3: iwxxm:originatingCentre must have a child element of iwxxm:WorldAreaForecastCentre</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <!-- Rule: Common.BasicReport-1 & Common.BasicReport-2 -->
    <xsl:if test="@permissibleUsage = 'NON-OPERATIONAL' and not(@permissibleUsageReason)">
      <svrl:failed-assert id="Common.BasicReport-1"
                          role="ERROR"
                          location="/iwxxm:WAFSSignificantWeatherForecast"
                          test="if (@permissibleUsage = 'NON-OPERATIONAL') then exists(@permissibleUsageReason) else true()">
        <svrl:text>Common.BasicReport-1: Non-operational reports must include a permissibleUsageReason</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <xsl:if test="(@permissibleUsage = 'OPERATIONAL' or not(@permissibleUsage)) and @permissibleUsageReason">
      <svrl:failed-assert id="Common.BasicReport-2"
                          role="ERROR"
                          location="/iwxxm:WAFSSignificantWeatherForecast"
                          test="if (@permissibleUsage = 'OPERATIONAL') then empty(@permissibleUsageReason) else true()">
        <svrl:text>Common.BasicReport-2: Operational reports should not include a permissibleUsageReason</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <!-- Rule: IWXXM-SIGWX-001 (Bounding Volume vertical limits) -->
    <xsl:if test="iwxxm:boundingVolume/iwxxm:ElevatedEnvelope">
      <xsl:variable name="lowerElev" select="number(iwxxm:boundingVolume/iwxxm:ElevatedEnvelope/iwxxm:lowerElevation)"/>
      <xsl:variable name="upperElev" select="number(iwxxm:boundingVolume/iwxxm:ElevatedEnvelope/iwxxm:upperElevation)"/>
      <xsl:if test="$upperElev &lt;= $lowerElev">
        <svrl:failed-assert id="IWXXM-SIGWX-001"
                            role="ERROR"
                            location="/iwxxm:WAFSSignificantWeatherForecast/iwxxm:boundingVolume/iwxxm:ElevatedEnvelope"
                            test="number(iwxxm:upperElevation) &gt; number(iwxxm:lowerElevation)">
          <svrl:text>[IWXXM-SIGWX-001] The upper elevation (<xsl:value-of select="$upperElev"/>) must be strictly greater than the lower elevation (<xsl:value-of select="$lowerElev"/>).</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:if>

    <!-- Rule: MeteorologicalFeature.MeteorologicalFeature-1 -->
    <xsl:for-each select="iwxxm:feature/iwxxm:MeteorologicalFeature">
      <xsl:if test="not(gml:identifier)">
        <svrl:failed-assert id="MeteorologicalFeature.MeteorologicalFeature-1"
                            role="ERROR"
                            location="/iwxxm:WAFSSignificantWeatherForecast/iwxxm:feature/iwxxm:MeteorologicalFeature[@gml:id='{@gml:id}']"
                            test="exists(gml:identifier)">
          <svrl:text>MeteorologicalFeature.MeteorologicalFeature-1: gml:identifier is mandatory</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>

    <!-- Rule: WAFSSignificantWeatherForecast.CloudDistribution (WMO BUFR 0-20-008 code list) -->
    <xsl:for-each select=".//iwxxm:CloudDistribution">
      <xsl:if test="not(@nilReason) and not(starts-with(string(@xlink:href), 'http://codes.wmo.int/bufr4/codeflag/0-20-008/'))">
        <svrl:failed-assert id="WAFSSignificantWeatherForecast.CloudDistribution"
                            role="ERROR"
                            location="//iwxxm:CloudDistribution"
                            test="starts-with(@xlink:href, 'http://codes.wmo.int/bufr4/codeflag/0-20-008/') or @nilReason">
          <svrl:text>Element in iwxxm:CloudDistribution should be a member of code list http://codes.wmo.int/bufr4/codeflag/0-20-008</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>

    <!-- Rule: WAFSSignificantWeatherForecast.CloudType (WMO BUFR 0-20-012 code list) -->
    <xsl:for-each select=".//iwxxm:CloudType">
      <xsl:if test="not(@nilReason) and not(starts-with(string(@xlink:href), 'http://codes.wmo.int/bufr4/codeflag/0-20-012/'))">
        <svrl:failed-assert id="WAFSSignificantWeatherForecast.CloudType"
                            role="ERROR"
                            location="//iwxxm:CloudType"
                            test="starts-with(@xlink:href, 'http://codes.wmo.int/bufr4/codeflag/0-20-012/') or @nilReason">
          <svrl:text>Element in iwxxm:CloudType should be a member of code list http://codes.wmo.int/bufr4/codeflag/0-20-012</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>

    <!-- Rule: WAFSSignificantWeatherForecast.DegreeOfTurbulence (WMO BUFR 0-11-030 code list) -->
    <xsl:for-each select=".//iwxxm:DegreeOfTurbulence">
      <xsl:if test="not(@nilReason) and not(starts-with(string(@xlink:href), 'http://codes.wmo.int/bufr4/codeflag/0-11-030/'))">
        <svrl:failed-assert id="WAFSSignificantWeatherForecast.DegreeOfTurbulence"
                            role="ERROR"
                            location="//iwxxm:DegreeOfTurbulence"
                            test="starts-with(@xlink:href, 'http://codes.wmo.int/bufr4/codeflag/0-11-030/') or @nilReason">
          <svrl:text>Element in iwxxm:DegreeOfTurbulence should be a member of code list http://codes.wmo.int/bufr4/codeflag/0-11-030</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>

    <!-- Rule: WAFSSignificantWeatherForecast.DegreeOfIcing (WMO BUFR 0-20-041 code list) -->
    <xsl:for-each select=".//iwxxm:DegreeOfIcing">
      <xsl:if test="not(@nilReason) and not(starts-with(string(@xlink:href), 'http://codes.wmo.int/bufr4/codeflag/0-20-041/'))">
        <svrl:failed-assert id="WAFSSignificantWeatherForecast.DegreeOfIcing"
                            role="ERROR"
                            location="//iwxxm:DegreeOfIcing"
                            test="starts-with(@xlink:href, 'http://codes.wmo.int/bufr4/codeflag/0-20-041/') or @nilReason">
          <svrl:text>Element in iwxxm:DegreeOfIcing should be a member of code list http://codes.wmo.int/bufr4/codeflag/0-20-041</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>

    <!-- Rule: IWXXM-SIGWX-002 (WAFSJetStreamWindSymbol speed >= 80 kt or >= 41.1 m/s) -->
    <xsl:for-each select="iwxxm:feature/iwxxm:MeteorologicalFeature/iwxxm:phenomenonProperty/iwxxm:WAFSJetStreamWindSymbol">
      <xsl:variable name="speed" select="number(iwxxm:windSpeed)"/>
      <xsl:variable name="uom" select="string(iwxxm:windSpeed/@uom)"/>
      <xsl:if test="($uom = '[kn_i]' and $speed &lt; 80) or ($uom = 'm/s' and $speed &lt; 41.0)">
        <svrl:failed-assert id="IWXXM-SIGWX-002"
                            role="ERROR"
                            location="//iwxxm:WAFSJetStreamWindSymbol[@gml:id='{@gml:id}']"
                            test="windSpeed &gt;= 80 kt (or 41.1 m/s)">
          <svrl:text>[IWXXM-SIGWX-002] WAFSJetStreamWindSymbol '<xsl:value-of select="@gml:id"/>' has wind speed <xsl:value-of select="$speed"/> (<xsl:value-of select="$uom"/>), below the ICAO Annex 3 minimum threshold of 80 kt (41.1 m/s).</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>
  </xsl:template>

  <!-- =========================================================================
       Compact SIGWXReport Business Rules
       ========================================================================= -->
  <xsl:template match="iwxxm:SIGWXReport">
    <svrl:active-pattern id="SIGWX-Core-Rules" name="SIGWX Core Meteorological Rules"/>

    <!-- Rule 1: SIGWX.VerticalLimits - Upper vertical limit must be strictly greater than lower limit -->
    <xsl:variable name="lowerFl" select="number(iwxxm:lowerLimit)"/>
    <xsl:variable name="upperFl" select="number(iwxxm:upperLimit)"/>
    <xsl:if test="$upperFl &lt;= $lowerFl">
      <svrl:failed-assert id="IWXXM-SIGWX-001"
                          role="ERROR"
                          location="/iwxxm:SIGWXReport"
                          test="number(iwxxm:upperLimit) &gt; number(iwxxm:lowerLimit)">
        <svrl:text>[IWXXM-SIGWX-001] The SIGWX report upper vertical limit (<xsl:value-of select="$upperFl"/>) must be strictly greater than the lower vertical limit (<xsl:value-of select="$lowerFl"/>).</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <!-- Rule 2: SIGWX.JetStreamSpeed - Every JetStream must have a wind speed >= 80 kt per ICAO Annex 3 -->
    <xsl:for-each select="iwxxm:jetStream">
      <xsl:variable name="windSpeed" select="number(iwxxm:windSpeed)"/>
      <xsl:variable name="windUom" select="string(iwxxm:windSpeed/@uom)"/>
      <xsl:if test="($windUom = '[kn_i]' and $windSpeed &lt; 80) or ($windUom = 'm/s' and $windSpeed &lt; 41.0)">
        <svrl:failed-assert id="IWXXM-SIGWX-002"
                            role="ERROR"
                            location="/iwxxm:SIGWXReport/iwxxm:jetStream[@gml:id='{@gml:id}']"
                            test="number(iwxxm:windSpeed) &gt;= 80">
          <svrl:text>[IWXXM-SIGWX-002] JetStream '<xsl:value-of select="@gml:id"/>' has maximum wind speed <xsl:value-of select="$windSpeed"/> kt, below the ICAO minimum threshold of 80 kt.</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>

    <!-- Rule 3: SIGWX.CloudVerticalExtent - Cloud top must be strictly greater than cloud base -->
    <xsl:for-each select="iwxxm:cloud">
      <xsl:if test="iwxxm:base">
        <xsl:variable name="baseFl" select="number(iwxxm:base)"/>
        <xsl:variable name="topFl" select="number(iwxxm:top)"/>
        <xsl:if test="$topFl &lt;= $baseFl">
          <svrl:failed-assert id="IWXXM-SIGWX-003"
                              role="ERROR"
                              location="/iwxxm:SIGWXReport/iwxxm:cloud[@gml:id='{@gml:id}']"
                              test="number(iwxxm:top) &gt; number(iwxxm:base)">
            <svrl:text>[IWXXM-SIGWX-003] Cloud distribution '<xsl:value-of select="@gml:id"/>' has a top flight level (<xsl:value-of select="$topFl"/>) less than or equal to its base (<xsl:value-of select="$baseFl"/>).</svrl:text>
          </svrl:failed-assert>
        </xsl:if>
      </xsl:if>
    </xsl:for-each>

    <!-- Rule 4: SIGWX.MeasureUom - Flight level elements must use unit of measure 'FL' -->
    <xsl:for-each select=".//iwxxm:flightLevel | iwxxm:lowerLimit | iwxxm:upperLimit">
      <xsl:if test="@uom != 'FL'">
        <svrl:failed-assert id="IWXXM-SIGWX-004"
                            role="ERROR"
                            location="{name(.)}"
                            test="@uom = 'FL'">
          <svrl:text>[IWXXM-SIGWX-004] Element '<xsl:value-of select="name(.)"/>' must use unit of measure 'FL' (Flight Level), but found '<xsl:value-of select="@uom"/>'.</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>
  </xsl:template>

</xsl:stylesheet>
