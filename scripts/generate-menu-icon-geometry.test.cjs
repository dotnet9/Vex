const assert = require('node:assert/strict');
const test = require('node:test');
const { independentPath, geometry } = require('./generate-menu-icon-geometry.cjs');

for (const [input, expected] of [
  ['M18 6 6 18', 'M18 6 6 18'],
  ['m6 6 12 12', 'M6 6l12 12'],
  ['m6 6 12 12 -2 -2', 'M6 6l12 12 -2 -2'],
  ['m6 6h12', 'M6 6 h12'],
  ['m6,6,12,12', 'M6 6l12,12'],
  ['m.5-.5 1e1-2', 'M.5 -.5l1e1-2'],
  ['m6 6 12 12 m1 1 2 2', 'M6 6l12 12 m1 1 2 2'],
]) {
  test(`Independent SVG path preserves its origin: ${input}`, () => {
    assert.equal(independentPath(input), expected);
  });
}

test('Close icon preserves both independent diagonal lines', () => {
  assert.equal(geometry('x'), 'M18 6 6 18 M6 6l12 12');
});

test('Both collapse chevrons start at independent SVG coordinates', () => {
  assert.equal(geometry('chevrons-up'), 'M17 11l-5-5-5 5 M17 18l-5-5-5 5');
});

test('Panel close arrow does not inherit the panel separator endpoint', () => {
  assert.match(geometry('panel-right-close'), / M8 9l3 3-3 3$/);
});
