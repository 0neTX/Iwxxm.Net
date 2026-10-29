<?xml version="1.0" encoding="UTF-8"?>
<!--
  Hand-written ISO Schematron-style XSLT 1.0 stylesheet for WMO/ICAO IWXXM 2025-2 reports
  and SWIM collect:MeteorologicalBulletin envelopes.

  PARTIAL IMPLEMENTATION: the official WMO rule set (IWXXM/rule/iwxxm.sch, 165 rule IDs) uses
  queryBinding="xslt2" and cannot be executed by System.Xml.Xsl.XslCompiledTransform (XSLT 1.0).
  This stylesheet re-implements only a subset of it in XSLT 1.0. Official rule IDs implemented here
  (11 of 165): Common.BasicReport-1, Common.BasicReport-2, MeteorologicalFeature.MeteorologicalFeature-1,
  MeteorologicalFeature.MeteorologicalFeatureCollection-1, WAFSSignificantWeatherForecast.CloudDistribution,
  WAFSSignificantWeatherForecast.CloudType, WAFSSignificantWeatherForecast.DegreeOfIcing,
  WAFSSignificantWeatherForecast.DegreeOfTurbulence, WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-1,
  WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-2, WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-3.
  All other rule IDs below are library-specific checks (IWXXM-SIGWX-*, *.Mandatory-*, IWXXM.RdfCodeListRegistry-1, ...).

  Code-list URIs (xlink:href starting with http://codes.wmo.int/) are checked against the 20 official
  WMO RDF/SKOS code lists embedded under ../Codelists/ (byte-identical copies of IWXXM/rule/*.rdf),
  selecting the official list from the URI register, with the same owl:versionInfo (IWXXM 2025-2)
  filter used by the official rules.

  Produces standard SVRL (Schematron Validation Report Language) output compatible with
  System.Xml.Xsl.XslCompiledTransform in .NET Standard 2.0.
-->
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:svrl="http://purl.oclc.org/dsdl/svrl"
                xmlns:iwxxm="http://icao.int/iwxxm/2025-2"
                xmlns:collect="http://def.wmo.int/collect/2014"
                xmlns:gml="http://www.opengis.net/gml/3.2"
                xmlns:xlink="http://www.w3.org/1999/xlink"
                xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                xmlns:skos="http://www.w3.org/2004/02/skos/core#"
                xmlns:owl="http://www.w3.org/2002/07/owl#"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

  <xsl:output method="xml" indent="yes" encoding="UTF-8"/>

  <!-- Official WMO RDF/SKOS code lists, resolved in-memory by the air-gapped EmbeddedXmlUrlResolver -->
  <xsl:variable name="iwxxmVersion" select="'http://icao.int/iwxxm/2025-2'"/>
  <xsl:variable name="bufrCloudDistribution" select="document('../Codelists/codes.wmo.int-bufr4-codeflag-0-20-008.rdf')"/>
  <xsl:variable name="bufrCloudType" select="document('../Codelists/codes.wmo.int-bufr4-codeflag-0-20-012.rdf')"/>
  <xsl:variable name="bufrTurbulence" select="document('../Codelists/codes.wmo.int-bufr4-codeflag-0-11-030.rdf')"/>
  <xsl:variable name="bufrIcing" select="document('../Codelists/codes.wmo.int-bufr4-codeflag-0-20-041.rdf')"/>

  <!-- Every embedded official code list (whitelist: no other document is ever requested) -->
  <xsl:variable name="officialCodeLists" select="concat(
    '|codes.wmo.int-49-2-AerodromePresentOrForecastWeather.rdf|codes.wmo.int-49-2-AerodromeRecentWeather.rdf',
    '|codes.wmo.int-49-2-AirWxPhenomena.rdf|codes.wmo.int-49-2-AviationColourCode.rdf',
    '|codes.wmo.int-49-2-CloudAmountReportedAtAerodrome.rdf|codes.wmo.int-49-2-MeteorologicalFeature.rdf',
    '|codes.wmo.int-49-2-SigConvectiveCloudType.rdf|codes.wmo.int-49-2-SigWxPhenomena.rdf',
    '|codes.wmo.int-49-2-SpaceWxLocation.rdf|codes.wmo.int-49-2-SpaceWxPhenomena.rdf',
    '|codes.wmo.int-49-2-WeatherCausingVisibilityReduction.rdf|codes.wmo.int-bufr4-codeflag-0-11-030.rdf',
    '|codes.wmo.int-bufr4-codeflag-0-20-008.rdf|codes.wmo.int-bufr4-codeflag-0-20-012.rdf',
    '|codes.wmo.int-bufr4-codeflag-0-20-041.rdf|codes.wmo.int-bufr4-codeflag-0-22-061.rdf',
    '|codes.wmo.int-common-nil.rdf|codes.wmo.int-iwxxm-AviationColourCode.rdf',
    '|codes.wmo.int-iwxxm-MeteorologicalFeature.rdf|codes.wmo.int-iwxxm-nil.rdf|')"/>

  <!-- Lists that the official iwxxm.sch filters by owl:versionInfo = IWXXM 2025-2 -->
  <xsl:variable name="versionedCodeLists" select="concat(
    '|codes.wmo.int-49-2-AerodromePresentOrForecastWeather.rdf|codes.wmo.int-49-2-AerodromeRecentWeather.rdf',
    '|codes.wmo.int-49-2-AirWxPhenomena.rdf|codes.wmo.int-49-2-CloudAmountReportedAtAerodrome.rdf',
    '|codes.wmo.int-49-2-SigConvectiveCloudType.rdf|codes.wmo.int-49-2-SigWxPhenomena.rdf',
    '|codes.wmo.int-49-2-SpaceWxLocation.rdf|codes.wmo.int-49-2-WeatherCausingVisibilityReduction.rdf',
    '|codes.wmo.int-iwxxm-AviationColourCode.rdf|codes.wmo.int-iwxxm-MeteorologicalFeature.rdf',
    '|codes.wmo.int-iwxxm-nil.rdf|')"/>

  <xsl:template match="/">
    <svrl:schematron-output title="IWXXM 2025-2 &amp; SWIM Business Rules Validation" schemaVersion="2025-2">
      <xsl:apply-templates select="*"/>
    </svrl:schematron-output>
  </xsl:template>

  <!-- =========================================================================
       Common Helper: Validate Common.BasicReport-1 & Common.BasicReport-2
       ========================================================================= -->
  <xsl:template name="ValidateCommonReportAttributes">
    <xsl:if test="@permissibleUsage = 'NON-OPERATIONAL' and not(@permissibleUsageReason)">
      <svrl:failed-assert id="Common.BasicReport-1"
                          role="ERROR"
                          location="{concat('/', name(.))}"
                          test="if (@permissibleUsage = 'NON-OPERATIONAL') then exists(@permissibleUsageReason) else true()">
        <svrl:text>Common.BasicReport-1: Non-operational reports must include a permissibleUsageReason</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <xsl:if test="@permissibleUsage = 'OPERATIONAL' and @permissibleUsageReason">
      <svrl:failed-assert id="Common.BasicReport-2"
                          role="ERROR"
                          location="{concat('/', name(.))}"
                          test="if (@permissibleUsage = 'OPERATIONAL') then empty(@permissibleUsageReason) else true()">
        <svrl:text>Common.BasicReport-2: Operational reports should not include a permissibleUsageReason</svrl:text>
      </svrl:failed-assert>
    </xsl:if>
  </xsl:template>

  <!-- =========================================================================
       Common Helper: Official WMO RDF/SKOS Code-List Validation
       Selects the official list from the URI register (and, for the shared
       http://codes.wmo.int/306/4678 register, from the element name), then
       requires the URI to be a skos:Concept of that list.
       ========================================================================= -->
  <xsl:template name="ResolveWmoCodeListFile">
    <xsl:param name="href"/>
    <xsl:param name="elementName"/>
    <xsl:choose>
      <xsl:when test="starts-with($href, 'http://codes.wmo.int/306/4678/')">
        <xsl:choose>
          <xsl:when test="$elementName = 'recentWeather'">codes.wmo.int-49-2-AerodromeRecentWeather.rdf</xsl:when>
          <xsl:otherwise>codes.wmo.int-49-2-AerodromePresentOrForecastWeather.rdf</xsl:otherwise>
        </xsl:choose>
      </xsl:when>
      <xsl:when test="starts-with($href, 'http://codes.wmo.int/49-2/')">
        <xsl:value-of select="concat('codes.wmo.int-49-2-', substring-before(substring-after($href, 'http://codes.wmo.int/49-2/'), '/'), '.rdf')"/>
      </xsl:when>
      <xsl:when test="starts-with($href, 'http://codes.wmo.int/bufr4/codeflag/')">
        <xsl:value-of select="concat('codes.wmo.int-bufr4-codeflag-', substring-before(substring-after($href, 'http://codes.wmo.int/bufr4/codeflag/'), '/'), '.rdf')"/>
      </xsl:when>
      <xsl:when test="starts-with($href, 'http://codes.wmo.int/common/nil/')">codes.wmo.int-common-nil.rdf</xsl:when>
      <xsl:when test="starts-with($href, 'http://codes.wmo.int/iwxxm/')">
        <xsl:value-of select="concat('codes.wmo.int-iwxxm-', substring-before(substring-after($href, 'http://codes.wmo.int/iwxxm/'), '/'), '.rdf')"/>
      </xsl:when>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="ValidateWmoRdfCodeList">
    <xsl:for-each select=".//*[@xlink:href]">
      <xsl:variable name="href" select="string(@xlink:href)"/>
      <xsl:if test="starts-with($href, 'http://codes.wmo.int/')">
        <xsl:variable name="codeListFileFragment">
          <xsl:call-template name="ResolveWmoCodeListFile">
            <xsl:with-param name="href" select="$href"/>
            <xsl:with-param name="elementName" select="local-name(.)"/>
          </xsl:call-template>
        </xsl:variable>
        <xsl:variable name="codeListFile" select="string($codeListFileFragment)"/>
        <xsl:choose>
          <xsl:when test="string-length($codeListFile) = 0 or not(contains($officialCodeLists, concat('|', $codeListFile, '|')))">
            <svrl:failed-assert id="IWXXM.RdfCodeListRegistry-1"
                                role="ERROR"
                                location="{concat('//', name(.))}"
                                test="@xlink:href is a member of an official WMO code list">
              <svrl:text>WMO code-list URI '<xsl:value-of select="$href"/>' on element '<xsl:value-of select="name(.)"/>' does not belong to any of the 20 official WMO RDF/SKOS code lists embedded in this library.</svrl:text>
            </svrl:failed-assert>
          </xsl:when>
          <xsl:otherwise>
            <xsl:variable name="concept" select="document(concat('../Codelists/', $codeListFile))//skos:Concept[@rdf:about = $href]"/>
            <xsl:variable name="isVersioned" select="contains($versionedCodeLists, concat('|', $codeListFile, '|'))"/>
            <xsl:if test="not($concept) or ($isVersioned and not($concept/owl:versionInfo[@rdf:resource = $iwxxmVersion]))">
              <svrl:failed-assert id="IWXXM.RdfCodeListRegistry-1"
                                  role="ERROR"
                                  location="{concat('//', name(.))}"
                                  test="@xlink:href = document('{$codeListFile}')//skos:Concept/@rdf:about">
                <svrl:text>WMO code-list URI '<xsl:value-of select="$href"/>' on element '<xsl:value-of select="name(.)"/>' is not a member of the official WMO code list <xsl:value-of select="$codeListFile"/><xsl:if test="$isVersioned"> registered for IWXXM 2025-2</xsl:if>.</svrl:text>
              </svrl:failed-assert>
            </xsl:if>
          </xsl:otherwise>
        </xsl:choose>
      </xsl:if>
    </xsl:for-each>
  </xsl:template>

  <!-- =========================================================================
       1. Official WMO IWXXM 2025-2 WAFSSignificantWeatherForecast Rules
       ========================================================================= -->
  <xsl:template match="iwxxm:WAFSSignificantWeatherForecast">
    <svrl:active-pattern id="WAFSSignificantWeatherForecast-Rules" name="WMO IWXXM 2025-2 WAFS SIGWX Rules"/>
    <xsl:call-template name="ValidateCommonReportAttributes"/>
    <xsl:call-template name="ValidateWmoRdfCodeList"/>

    <xsl:if test="not(gml:identifier)">
      <svrl:failed-assert id="MeteorologicalFeature.MeteorologicalFeatureCollection-1"
                          role="ERROR"
                          location="/iwxxm:WAFSSignificantWeatherForecast"
                          test="exists(gml:identifier)">
        <svrl:text>MeteorologicalFeature.MeteorologicalFeatureCollection-1: gml:identifier is mandatory</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <xsl:if test="string(iwxxm:phenomenonCategory) != 'weatherForecasts'">
      <svrl:failed-assert id="WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-1"
                          role="ERROR"
                          location="/iwxxm:WAFSSignificantWeatherForecast/iwxxm:phenomenonCategory"
                          test="iwxxm:phenomenonCategory = 'weatherForecasts'">
        <svrl:text>WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-1: iwxxm:phenomenonCategory shall be equal to 'weatherForecasts'.</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <xsl:if test="not(iwxxm:phenomenonBaseTime)">
      <svrl:failed-assert id="WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-2"
                          role="ERROR"
                          location="/iwxxm:WAFSSignificantWeatherForecast"
                          test="exists(iwxxm:phenomenonBaseTime)">
        <svrl:text>WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-2: iwxxm:phenomenonBaseTime is mandatory</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <xsl:if test="not(iwxxm:originatingCentre/iwxxm:WorldAreaForecastCentre)">
      <svrl:failed-assert id="WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-3"
                          role="ERROR"
                          location="/iwxxm:WAFSSignificantWeatherForecast/iwxxm:originatingCentre"
                          test="exists(iwxxm:originatingCentre/iwxxm:WorldAreaForecastCentre)">
        <svrl:text>WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-3: iwxxm:originatingCentre must have a child element of iwxxm:WorldAreaForecastCentre</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

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

    <xsl:for-each select=".//iwxxm:CloudDistribution">
      <xsl:if test="not(@nilReason) and not(@xlink:href = $bufrCloudDistribution//skos:Concept/@rdf:about)">
        <svrl:failed-assert id="WAFSSignificantWeatherForecast.CloudDistribution"
                            role="ERROR"
                            location="//iwxxm:CloudDistribution"
                            test="@xlink:href = document('codes.wmo.int-bufr4-codeflag-0-20-008.rdf')/rdf:RDF//skos:member/skos:Concept/@rdf:about or @nilReason">
          <svrl:text>Element in iwxxm:CloudDistribution should be a member of code list http://codes.wmo.int/bufr4/codeflag/0-20-008</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>

    <xsl:for-each select=".//iwxxm:CloudType">
      <xsl:if test="not(@nilReason) and not(@xlink:href = $bufrCloudType//skos:Concept/@rdf:about)">
        <svrl:failed-assert id="WAFSSignificantWeatherForecast.CloudType"
                            role="ERROR"
                            location="//iwxxm:CloudType"
                            test="@xlink:href = document('codes.wmo.int-bufr4-codeflag-0-20-012.rdf')/rdf:RDF//skos:member/skos:Concept/@rdf:about or @nilReason">
          <svrl:text>Element in iwxxm:CloudType should be a member of code list http://codes.wmo.int/bufr4/codeflag/0-20-012</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>

    <xsl:for-each select=".//iwxxm:DegreeOfTurbulence">
      <xsl:if test="not(@nilReason) and not(@xlink:href = $bufrTurbulence//skos:Concept/@rdf:about)">
        <svrl:failed-assert id="WAFSSignificantWeatherForecast.DegreeOfTurbulence"
                            role="ERROR"
                            location="//iwxxm:DegreeOfTurbulence"
                            test="@xlink:href = document('codes.wmo.int-bufr4-codeflag-0-11-030.rdf')/rdf:RDF//skos:member/skos:Concept/@rdf:about or @nilReason">
          <svrl:text>Element in iwxxm:DegreeOfTurbulence should be a member of code list http://codes.wmo.int/bufr4/codeflag/0-11-030</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>

    <xsl:for-each select=".//iwxxm:DegreeOfIcing">
      <xsl:if test="not(@nilReason) and not(@xlink:href = $bufrIcing//skos:Concept/@rdf:about)">
        <svrl:failed-assert id="WAFSSignificantWeatherForecast.DegreeOfIcing"
                            role="ERROR"
                            location="//iwxxm:DegreeOfIcing"
                            test="@xlink:href = document('codes.wmo.int-bufr4-codeflag-0-20-041.rdf')/rdf:RDF//skos:member/skos:Concept/@rdf:about or @nilReason">
          <svrl:text>Element in iwxxm:DegreeOfIcing should be a member of code list http://codes.wmo.int/bufr4/codeflag/0-20-041</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>

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
       2. Compact SIGWXReport Business Rules (NON-STANDARD extension root)
       ========================================================================= -->
  <xsl:template match="iwxxm:SIGWXReport">
    <svrl:active-pattern id="SIGWX-Core-Rules" name="SIGWX Core Meteorological Rules"/>
    <xsl:call-template name="ValidateCommonReportAttributes"/>

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

  <!-- =========================================================================
       3. VONA & QVACI Rules (VolcanoObservatoryNoticeForAviation & QVACI)
       ========================================================================= -->
  <xsl:template match="iwxxm:VolcanoObservatoryNoticeForAviation">
    <svrl:active-pattern id="VONA-Rules" name="WMO IWXXM 2025-2 VONA Business Rules"/>
    <xsl:call-template name="ValidateCommonReportAttributes"/>
    <xsl:call-template name="ValidateWmoRdfCodeList"/>

    <xsl:if test="not(@translationFailedTAC) and string(iwxxm:phenomenonCategory) != 'volcanicObservations'">
      <svrl:failed-assert id="VONA.VONA-1"
                          role="ERROR"
                          location="/iwxxm:VolcanoObservatoryNoticeForAviation/iwxxm:phenomenonCategory"
                          test="iwxxm:phenomenonCategory = 'volcanicObservations'">
        <svrl:text>VONA.VONA-1: iwxxm:phenomenonCategory in VolcanoObservatoryNoticeForAviation shall be 'volcanicObservations'.</svrl:text>
      </svrl:failed-assert>
    </xsl:if>
  </xsl:template>

  <xsl:template match="iwxxm:QuantitativeVolcanicAshConcentrationInformation">
    <svrl:active-pattern id="QVACI-Rules" name="WMO IWXXM 2025-2 QVACI Business Rules"/>
    <xsl:call-template name="ValidateCommonReportAttributes"/>
    <xsl:call-template name="ValidateWmoRdfCodeList"/>

    <xsl:if test="not(@translationFailedTAC) and string(iwxxm:phenomenonCategory) != 'volcanicForecasts'">
      <svrl:failed-assert id="QVACI.QVACI-1"
                          role="ERROR"
                          location="/iwxxm:QuantitativeVolcanicAshConcentrationInformation/iwxxm:phenomenonCategory"
                          test="iwxxm:phenomenonCategory = 'volcanicForecasts'">
        <svrl:text>QVACI.QVACI-1: iwxxm:phenomenonCategory in QuantitativeVolcanicAshConcentrationInformation shall be 'volcanicForecasts'.</svrl:text>
      </svrl:failed-assert>
    </xsl:if>

    <xsl:for-each select=".//iwxxm:ashDensityEqualsOrExceeds">
      <xsl:if test="number(.) &lt;= 0">
        <svrl:failed-assert id="QVACI.AshDensity-1"
                            role="ERROR"
                            location="//iwxxm:ashDensityEqualsOrExceeds"
                            test="number(.) &gt; 0">
          <svrl:text>QVACI.AshDensity-1: Ash concentration density must be strictly positive (mg/m3).</svrl:text>
        </svrl:failed-assert>
      </xsl:if>
    </xsl:for-each>
  </xsl:template>

  <!-- =========================================================================
       4. Aerodrome Reports Rules (METAR, SPECI, TAF)
       ========================================================================= -->
  <xsl:template match="iwxxm:METAR | iwxxm:SPECI">
    <svrl:active-pattern id="METAR-SPECI-Rules" name="WMO IWXXM 2025-2 METAR/SPECI Business Rules"/>
    <xsl:call-template name="ValidateCommonReportAttributes"/>
    <xsl:call-template name="ValidateWmoRdfCodeList"/>

    <xsl:if test="not(@translationFailedTAC) and not(iwxxm:observation)">
      <svrl:failed-assert id="METAR_SPECI.ObservationMandatory-1"
                          role="ERROR"
                          location="{concat('/', name(.))}"
                          test="exists(iwxxm:observation) or exists(@translationFailedTAC)">
        <svrl:text>METAR_SPECI.ObservationMandatory-1: iwxxm:observation is required when report is not a failed TAC translation.</svrl:text>
      </svrl:failed-assert>
    </xsl:if>
  </xsl:template>

  <xsl:template match="iwxxm:TAF">
    <svrl:active-pattern id="TAF-Rules" name="WMO IWXXM 2025-2 TAF Business Rules"/>
    <xsl:call-template name="ValidateCommonReportAttributes"/>
    <xsl:call-template name="ValidateWmoRdfCodeList"/>

    <xsl:if test="not(@translationFailedTAC) and not(@isCancelReport = 'true') and not(iwxxm:baseForecast)">
      <svrl:failed-assert id="TAF.BaseForecastMandatory-1"
                          role="ERROR"
                          location="/iwxxm:TAF"
                          test="exists(iwxxm:baseForecast) or @isCancelReport = 'true' or exists(@translationFailedTAC)">
        <svrl:text>TAF.BaseForecastMandatory-1: iwxxm:baseForecast is required unless isCancelReport is true or translationFailedTAC is present.</svrl:text>
      </svrl:failed-assert>
    </xsl:if>
  </xsl:template>

  <!-- =========================================================================
       5. En-Route & Advisory Reports Rules (SIGMET, AIRMET, Advisories, Bulletin)
       ========================================================================= -->
  <xsl:template match="iwxxm:SIGMET | iwxxm:VolcanicAshSIGMET | iwxxm:TropicalCycloneSIGMET | iwxxm:AIRMET | iwxxm:VolcanicAshAdvisory | iwxxm:TropicalCycloneAdvisory | iwxxm:SpaceWeatherAdvisory">
    <svrl:active-pattern id="EnRoute-Advisory-Rules" name="WMO IWXXM 2025-2 En-Route and Advisory Rules"/>
    <xsl:call-template name="ValidateCommonReportAttributes"/>
    <xsl:call-template name="ValidateWmoRdfCodeList"/>
  </xsl:template>

  <xsl:template match="collect:MeteorologicalBulletin">
    <svrl:active-pattern id="Collect-Bulletin-Rules" name="WMO Collect 2014 MeteorologicalBulletin Rules"/>
    <xsl:if test="string-length(normalize-space(collect:bulletinIdentifier)) = 0">
      <svrl:failed-assert id="COLLECT.BulletinIdentifier-1"
                          role="ERROR"
                          location="/collect:MeteorologicalBulletin/collect:bulletinIdentifier"
                          test="string-length(normalize-space(collect:bulletinIdentifier)) &gt; 0">
        <svrl:text>COLLECT.BulletinIdentifier-1: collect:bulletinIdentifier must not be empty.</svrl:text>
      </svrl:failed-assert>
    </xsl:if>
    <xsl:apply-templates select="collect:meteorologicalInformation/*"/>
  </xsl:template>

</xsl:stylesheet>
