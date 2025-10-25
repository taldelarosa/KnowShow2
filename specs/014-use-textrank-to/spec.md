# Feature Specification: TextRank-Based Semantic Subtitle Matching

**Feature Branch**: `014-use-textrank-to`
**Created**: 2025-10-24
**Status**: Ready for Planning
**Input**: User description: "Use TextRank to extract the most plot-relevant sentences from each subtitle file, then embed those with MiniLM to compare semantic content across files. This filters out filler dialogue and focuses matching on core storyline elements"

## Executive Summary

This feature enhances episode identification accuracy by extracting only the most plot-relevant content from subtitle files before semantic comparison. By applying TextRank algorithm to identify key sentences that drive the narrative, the system can focus matching on meaningful dialogue while ignoring conversational filler, character banter, and repetitive phrases that don't contribute to episode identification.

## User Scenarios & Testing

### Primary User Story

As a media librarian, when I identify an episode using subtitle files that contain verbose dialogue with lots of small talk and repeated phrases, the system extracts only the plot-critical sentences (major revelations, key actions, important character interactions) and performs semantic matching on those sentences, resulting in more accurate episode identification even when subtitle files differ significantly in conversational padding or translation style.

### Acceptance Scenarios

1. **Given** a subtitle file with 500 dialogue lines (300 filler, 200 plot-relevant), **When** the user identifies an episode, **Then** the system extracts the top 25% of sentences by TextRank score (configurable via `textRankSentencePercentage` setting) and uses only those for embedding-based matching

2. **Given** two subtitle files for the same episode where one is verbose (includes all small talk) and the other is condensed (plot-focused translation), **When** the system compares them, **Then** both files yield similar sets of high-scoring sentences resulting in a match confidence above the threshold

3. **Given** a subtitle file with repetitive character catchphrases or formulaic dialogue (e.g., "Let's go", "Are you okay?"), **When** TextRank scoring is applied, **Then** these low-information sentences receive lower scores and are excluded from the final embedding comparison

4. **Given** an existing subtitle database with full-text embeddings from feature 013, **When** the user enables TextRank filtering, **Then** the system regenerates embeddings on-demand during matching (no migration required), with legacy embeddings automatically replaced when episodes are re-identified

### Edge Cases

- What happens when TextRank extraction yields too few sentences (e.g., <10 sentences from a 30-minute episode)? **System falls back to full-text embedding if extracted sentences < 15 or < 10% of original content (whichever is larger)**
- How does the system handle subtitle files with unusual formatting (all caps, no punctuation, stage directions mixed with dialogue) that may confuse sentence segmentation?
- What happens if two different episodes have similar plot-critical sentences (e.g., procedural shows with formulaic plots)?
- How does system performance scale when processing very large subtitle files (e.g., 2+ hour movies with thousands of lines)?

## Requirements

### Functional Requirements

- **FR-001**: System MUST extract text content from subtitle files and segment it into individual sentences using sentence boundary detection
- **FR-002**: System MUST apply TextRank graph-based ranking algorithm to score each sentence by its semantic importance within the subtitle document
- **FR-003**: System MUST select top 25% of sentences (configurable via `textRankSentencePercentage`, range: 10-50%) based on TextRank scores for embedding generation
- **FR-004**: System MUST generate semantic embeddings only for the extracted high-scoring sentences, not the full subtitle text
- **FR-005**: System MUST use the filtered embeddings for episode matching comparisons via cosine similarity search
- **FR-006**: System MUST regenerate embeddings on-demand when TextRank mode is enabled, with no bulk migration required (lazy update strategy)
- **FR-007**: System MUST provide configuration option to enable/disable TextRank filtering, allowing fallback to full-text embedding mode
- **FR-008**: System MUST log statistics about TextRank extraction (total sentences, selected sentences, average score) for debugging and tuning
- **FR-009**: System MUST fall back to full-text embedding mode when TextRank yields fewer than 15 sentences OR fewer than 10% of original sentence count (whichever is larger)
- **FR-010**: System MUST preserve existing subtitle format support (Text/SRT, PGS, VobSub) with TextRank applied after OCR processing for image-based formats

### Key Entities

- **Ranked Sentence**: A sentence extracted from subtitle content with an associated TextRank importance score (0.0-1.0), used to determine whether it should be included in embedding generation
- **Plot-Relevant Embedding**: A semantic embedding vector generated from the concatenated set of high-scoring sentences, representing the core narrative content of a subtitle file
- **TextRank Configuration**: Settings controlling sentence extraction behavior, including score threshold percentage, minimum sentence count, and whether filtering is enabled

## Clarification Decisions

### 1. Sentence Selection Threshold

**Decision**: Default to top 25% of sentences by TextRank score

- **Rationale**: Balances signal retention with noise reduction; 25% typically captures key plot points without excessive filler
- **Configuration**: `textRankSentencePercentage` (range: 10-50%) allows tuning per use case
- **Example**: 500-line subtitle → 125 sentences selected for embedding

### 2. Migration Strategy

**Decision**: Lazy on-demand regeneration, no bulk migration

- **Rationale**: Avoids expensive upfront migration; embeddings update naturally during episode identification
- **Behavior**: When TextRank is enabled, new embeddings replace old ones as files are processed
- **Compatibility**: Existing full-text embeddings remain functional until regenerated

### 3. Minimum Sentence Threshold

**Decision**: Fallback to full-text embedding if extracted < 15 sentences OR < 10% of original

- **Rationale**: Very short extractions lose too much context; dual threshold handles both short and long files
- **Examples**:
    - 50-line file → 25% = 12 sentences → FALLBACK (< 15)
    - 1000-line file → 25% = 250 sentences → OK
    - 200-line file with weak TextRank scores → 8 sentences (4%) → FALLBACK (< 10%)

### 4. Dual Index Strategy

**Decision**: Single embedding column, lazy update (no dual indexes)

- **Rationale**: Simpler schema, lower storage cost, natural migration path
- **Trade-off**: Cannot compare TextRank vs full-text embeddings side-by-side without regeneration
- **Future**: If A/B testing needed, can be added via separate metadata column

## Review & Acceptance Checklist

### Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

### Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain - **All 4 clarifications resolved**
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Dependencies & Assumptions

### Dependencies

- Feature 013 (ML embedding matching) must be completed and stable
- Existing subtitle extraction and OCR pipelines remain functional
- Sentence boundary detection capability (period/newline-based segmentation)

### Assumptions

- TextRank algorithm is suitable for identifying plot-relevant content in subtitle text
- 20-30% sentence retention provides sufficient signal for accurate episode matching
- Filtering out conversational filler improves rather than degrades matching accuracy
- Processing overhead of TextRank scoring is acceptable for the improved accuracy

## Execution Status

- [x] User description parsed
- [x] Key concepts extracted (TextRank, sentence extraction, semantic filtering, plot-relevant matching)
- [x] Ambiguities marked (4 clarification points identified)
- [x] User scenarios defined
- [x] Requirements generated
- [x] Entities identified
- [x] All clarifications resolved with concrete decisions
- [x] Review checklist passed - **SPEC READY FOR PLANNING**
