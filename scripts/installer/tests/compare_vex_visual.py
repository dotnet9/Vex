"""Compare equal-scale screenshots and retain both raw and raster-tolerant metrics.

Pillow is a development-only dependency. Production never reads HTML/screenshots.
The normalized RGB error is a bounded visual regression metric, not a claim that
all operating systems, fonts or interactions have 98% subjective equivalence.
"""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageStat


def similarity(a, b, mask, blur=0):
    if blur:
        a = a.filter(ImageFilter.GaussianBlur(blur))
        b = b.filter(ImageFilter.GaussianBlur(blur))
    mean = ImageStat.Stat(ImageChops.difference(a, b), mask).mean
    return round(100 * (1 - sum(mean) / (3 * 255)), 4)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--native', type=Path, required=True)
    parser.add_argument('--languages', nargs='+', choices=('zh', 'en'), default=['zh'])
    parser.add_argument('--pages', nargs='+', choices=('welcome', 'custom', 'license', 'progress', 'complete', 'maintenance'),
                        default=['welcome', 'custom', 'progress', 'complete', 'maintenance'])
    args = parser.parse_args()
    results = []
    pages = {'welcome': '01-welcome', 'custom': '02-custom', 'license': '04-license',
             'progress': '05-progress', 'complete': '03-complete', 'maintenance': '02-maintenance'}
    for lang in args.languages:
        for theme in ('light', 'dark'):
            for page, filename in pages.items():
                if page not in args.pages:
                    continue
                expected = Image.open(args.reference / f'{lang}-{theme}-{page}.png').convert('RGB')
                folder = f'{lang}-{theme}' + ('-maintenance' if page == 'maintenance' else '')
                actual = Image.open(args.native / folder / f'{filename}.png').convert('RGB')
                if expected.width != actual.width or abs(expected.height - actual.height) > 1:
                    raise AssertionError(f'{folder}/{page}: size {actual.size} != {expected.size}')
                width, height = expected.width, min(expected.height, actual.height)
                expected, actual = expected.crop((0, 0, width, height)), actual.crop((0, 0, width, height))
                mask = Image.new('L', expected.size, 255)
                draw = ImageDraw.Draw(mask)
                # Region-clipped desktop corners are not window content. Native
                # edge visibility is checked independently on all four sides.
                for box in ((0, 0, 16, 16), (width-17, 0, width, 16),
                            (0, height-17, 16, height), (width-27, height-27, width, height)):
                    draw.rectangle(box, fill=0)
                if page == 'custom':
                    # The real temporary installation path is machine-specific.
                    draw.rectangle((234, 148, 520, 168), fill=0)
                if page == 'maintenance':
                    draw.rectangle((222, 151, 598, 178), fill=0)
                raw = similarity(expected, actual, mask)
                tolerant = similarity(expected, actual, mask, blur=1)
                results.append(dict(language=lang, theme=theme, page=page, raw_rgb_percent=raw,
                                    raster_tolerant_rgb_percent=tolerant, passed=tolerant >= 98))
                print(f'{lang}/{theme}/{page}: raw {raw:.2f}%, 1px raster tolerance {tolerant:.2f}%')
    report = dict(metric='1 - mean absolute RGB error / 255; 1px Gaussian raster tolerance',
                  threshold_percent=98, masks=['outside window corners', 'machine-specific path text'],
                  coverage=dict(languages=args.languages, pages=args.pages,
                                exclusions='The Chinese prototype has no English state; full MIT text is verified by native interaction.'),
                  comparisons=results, passed=all(row['passed'] for row in results))
    (args.native / 'visual-comparison.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    if not report['passed']:
        raise AssertionError('One or more visual comparisons fell below 98%; see visual-comparison.json')


if __name__ == '__main__':
    main()
