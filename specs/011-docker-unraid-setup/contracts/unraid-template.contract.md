# Contract: Unraid Docker Template

**Purpose**: Define XML template structure for Unraid Docker UI  
**Date**: 2025-10-11

## Template Schema

The Unraid template XML MUST conform to Unraid Community Applications schema version 2.

### Required Root Elements

```xml
<?xml version="1.0"?>
<Container version="2">
  <Name>Episode Identifier</Name>
  <Repository>taldelarosa/episode-identifier</Repository>
  <Registry>https://hub.docker.com/r/taldelarosa/episode-identifier</Registry>
  <Network>bridge</Network>
  <Privileged>false</Privileged>
  <Support>https://github.com/taldelarosa/KnowShow2/issues</Support>
  <Project>https://github.com/taldelarosa/KnowShow2</Project>
  <Overview>Identifies TV episodes from video files using PGS subtitle matching</Overview>
  <Category>MediaApp:Video Tools:</Category>
  <WebUI/>
  <TemplateURL/>
  <Icon>https://raw.githubusercontent.com/taldelarosa/KnowShow2/main/docs/icon.png</Icon>
  <ExtraParams/>
  <Description>...</Description>
  
  <!-- Volume Configs -->
  <Config Name="Videos" Target="/videos" ... />
  <Config Name="Database" Target="/data" ... />
  <Config Name="Config" Target="/config" ... />
  
  <!-- Environment Configs -->
  <Config Name="PUID" Target="PUID" ... />
  <Config Name="PGID" Target="PGID" ... />
  <Config Name="Timezone" Target="TZ" ... />
</Container>
```

---

## Volume Configuration Contracts

### Videos Volume

```xml
<Config 
  Name="Videos Directory" 
  Target="/videos" 
  Default="/mnt/user/media/videos" 
  Mode="rw" 
  Description="Path to video files for processing. Read/write access required for rename functionality." 
  Type="Path" 
  Display="always" 
  Required="false" 
  Mask="false"
/>
```

**Contract**:

- `Target` MUST be `/videos` (container path)
- `Mode` MUST be `rw` for rename support
- `Required` is `false` (can use --input with absolute paths)

---

### Database Volume

```xml
<Config 
  Name="Database Directory" 
  Target="/data" 
  Default="/mnt/user/appdata/episode-identifier" 
  Mode="rw" 
  Description="Persistent storage for subtitle hash database. Essential for remembering learned episodes." 
  Type="Path" 
  Display="always" 
  Required="true" 
  Mask="false"
/>
```

**Contract**:

- `Target` MUST be `/data`
- `Mode` MUST be `rw`
- `Required` is `true` (critical for persistence)

---

### Config Volume

```xml
<Config 
  Name="Config Directory" 
  Target="/config" 
  Default="/mnt/user/appdata/episode-identifier/config" 
  Mode="ro" 
  Description="Optional: Custom configuration JSON file location. Uses defaults if not provided." 
  Type="Path" 
  Display="advanced" 
  Required="false" 
  Mask="false"
/>
```

**Contract**:

- `Target` MUST be `/config`
- `Mode` MUST be `ro` (read-only)
- `Display` is `advanced` (not shown by default)

---

## Environment Variable Contracts

### PUID

```xml
<Config 
  Name="PUID" 
  Target="PUID" 
  Default="99" 
  Mode="" 
  Description="User ID for file operations. Default 99 (nobody) works for most Unraid setups." 
  Type="Variable" 
  Display="advanced" 
  Required="true" 
  Mask="false"
/>
```

---

### PGID

```xml
<Config 
  Name="PGID" 
  Target="PGID" 
  Default="100" 
  Mode="" 
  Description="Group ID for file operations. Default 100 (users) works for most Unraid setups." 
  Type="Variable" 
  Display="advanced" 
  Required="true" 
  Mask="false"
/>
```

---

### Timezone

```xml
<Config 
  Name="Timezone" 
  Target="TZ" 
  Default="UTC" 
  Mode="" 
  Description="Timezone for log timestamps (e.g., America/New_York)" 
  Type="Variable" 
  Display="advanced" 
  Required="false" 
  Mask="false"
/>
```

---

## Description Field Contract

The `Description` element MUST include:

1. **Purpose**: What the application does
2. **Key Features**: Main capabilities (PGS extraction, fuzzy hashing, bulk processing)
3. **Usage Instructions**: How to execute commands via docker exec
4. **Volume Requirements**: What each volume is for
5. **Example Command**: Basic usage example

**Maximum Length**: 2000 characters (Unraid limitation)

---

## Icon Requirements

**Icon URL**: Must be publicly accessible HTTPS URL  
**Format**: PNG  
**Size**: 256x256 pixels recommended  
**Transparency**: Supported

---

## Validation Tests

### Template Validation

1. XML is well-formed (valid XML syntax)
2. All required elements present
3. Target paths start with `/`
4. Default host paths use `/mnt/user/` (Unraid convention)
5. Variable defaults are valid values

### UI Rendering Test

1. Template loads in Unraid UI without errors
2. All volumes shown in "Path" section
3. All variables shown in "Variables" section (advanced collapsed)
4. Description renders with proper formatting
5. Icon displays correctly

---

## Example Complete Template

See: `/mnt/c/Users/Ragma/KnowShow_Specd/docker/unraid-template.xml` (to be created)

---

## References

- [Unraid Template Schema](https://github.com/Squidly271/docker.templates)
- [Community Applications Guidelines](https://forums.unraid.net/topic/57181-docker-faq/)
