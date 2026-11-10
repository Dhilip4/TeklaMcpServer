# Model Checker Rules

Place automated BIM validation rules here. These rules will be used by the AI Model Checker to validate Tekla models against your company and engineering standards.

## What Belongs Here
- Naming convention validation rules
- Profile/material compliance checks
- Bolt spacing and edge distance rules
- Weld size validation rules
- Clash detection criteria
- Phase assignment rules
- Drawing completeness checks

## File Naming Convention
- `rule_naming.md` — Part naming validation rules
- `rule_bolts.md` — Bolt size/spacing/edge distance checks
- `rule_welds.md` — Weld size adequacy checks
- `rule_profiles.md` — Profile and material compliance
- `rule_drawings.md` — Drawing completeness and quality checks
- `rule_clashes.md` — Clash and interference rules

## Rule Format
Each rule file should follow this structure:
```markdown
## Rule: [Rule Name]
- **Category**: Bolts / Welds / Naming / Geometry / Drawings
- **Severity**: Error / Warning / Info
- **Check**: [What to inspect]
- **Pass Condition**: [When the check passes]
- **Fail Message**: [What to report when it fails]
- **Reference**: [Engineering code or company standard]
```
