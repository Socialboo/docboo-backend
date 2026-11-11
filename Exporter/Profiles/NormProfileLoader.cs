using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exporter.Profiles;

/// <summary>Parses and validates norm profile JSON. Invalid profiles fail at startup, not during an export.</summary>
public static class NormProfileLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
    };

    public static NormProfile Parse(string json)
    {
        NormProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<NormProfile>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidNormProfileException($"Invalid profile JSON: {ex.Message}", ex);
        }

        if (profile is null) throw new InvalidNormProfileException("Profile JSON is null.");
        Validate(profile);
        return profile;
    }

    private static void Validate(NormProfile p)
    {
        var errors = new List<string>();
        void Check(bool ok, string message) { if (!ok) errors.Add(message); }

        Check(!string.IsNullOrWhiteSpace(p.Id), "id is required");
        Check(p.MaxSectionDepth is >= 1 and <= 9, "maxSectionDepth must be 1..9");
        Check(!string.IsNullOrWhiteSpace(p.Language), "language is required");

        var page = p.Page;
        Check(page.WidthCm is > 5 and < 100 && page.HeightCm is > 5 and < 100, "page size out of range");
        Check(new[] { page.MarginTopCm, page.MarginLeftCm, page.MarginBottomCm, page.MarginRightCm }.All(m => m is >= 0 and < 10),
            "margins must be 0..10 cm");
        Check(page.MarginLeftCm + page.MarginRightCm < page.WidthCm - 5, "horizontal margins leave no text area");
        Check(page.MarginTopCm + page.MarginBottomCm < page.HeightCm - 5, "vertical margins leave no text area");
        Check(page.HeaderDistanceCm is > 0 && page.HeaderDistanceCm < page.MarginTopCm, "headerDistanceCm must be inside the top margin");
        Check(page.FooterDistanceCm is > 0 && page.FooterDistanceCm < page.MarginBottomCm, "footerDistanceCm must be inside the bottom margin");

        Check(p.Fonts.Families.Count > 0, "fonts.families must not be empty");
        Check(p.Fonts.Families.ContainsKey(p.Fonts.Default), "fonts.default must be a key of fonts.families");
        Check(p.Fonts.Families.Keys.All(k => k is "timesNewRoman" or "arial"), "fonts.families keys must be timesNewRoman or arial");

        foreach (var (name, size, spacing) in new[]
                 {
                     ("body", p.Body.FontSizePt, p.Body.LineSpacing),
                     ("smallText", p.SmallText.FontSizePt, p.SmallText.LineSpacing),
                     ("longQuote", p.LongQuote.FontSizePt, p.LongQuote.LineSpacing),
                 })
        {
            Check(size is >= 6 and <= 24, $"{name}.fontSizePt must be 6..24");
            Check(spacing is >= 1 and <= 3, $"{name}.lineSpacing must be 1..3");
        }
        Check(p.Body.FirstLineIndentCm is >= 0 and <= 5, "body.firstLineIndentCm must be 0..5");
        Check(p.LongQuote.LeftIndentCm is >= 0 and <= 10, "longQuote.leftIndentCm must be 0..10");
        Check(p.PageNumbering.FontSizePt is >= 6 and <= 24, "pageNumbering.fontSizePt must be 6..24");
        Check(p.References.LineSpacing is >= 1 and <= 3, "references.lineSpacing must be 1..3");
        Check(p.References.SpaceAfterPt is >= 0 and <= 48, "references.spaceAfterPt must be 0..48");
        Check(!string.IsNullOrWhiteSpace(p.Captions.FigureLabel) && !string.IsNullOrWhiteSpace(p.Captions.TableLabel),
            "captions labels are required");
        Check(p.Captions.FigureLabel != p.Captions.TableLabel, "captions.figureLabel and tableLabel must differ");
        Check(p.Captions.FigureLabel.All(char.IsLetter) && p.Captions.TableLabel.All(char.IsLetter),
            "captions labels must be single words (they name the SEQ fields)");

        var levels = p.Headings.Levels.Select(l => l.Level).ToList();
        Check(levels.SequenceEqual(Enumerable.Range(1, p.MaxSectionDepth)),
            $"headings.levels must define levels 1..{p.MaxSectionDepth} in order");

        Check(p.PreTextual.NatureIndentCm >= 0 && p.PreTextual.NatureIndentCm < page.WidthCm - page.MarginLeftCm - page.MarginRightCm,
            "preTextual.natureIndentCm must be inside the text area");

        if (errors.Count > 0)
            throw new InvalidNormProfileException($"Invalid norm profile '{p.Id}': {string.Join("; ", errors)}.");
    }
}

public sealed class InvalidNormProfileException(string message, Exception? inner = null) : Exception(message, inner);
