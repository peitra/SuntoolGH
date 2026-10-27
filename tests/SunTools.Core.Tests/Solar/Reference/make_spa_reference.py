"""Generate spa_reference.csv: 1,000 sun positions from pvlib's independent SPA implementation.

Usage (any Python with pvlib==0.16.1, see requirements.txt):
    python make_spa_reference.py

800 general points (years 1900-2100, any latitude, longitude, elevation, pressure, temperature,
time zone and delta T) and 200 points within 2 degrees of the horizon, on both sides of the
refraction switch. Times are written as local standard time plus time zone, as in an EPW file.
"""
import csv
import datetime as dt
import os

import numpy as np
from pvlib import spa

SEED = 20261027
REFRACTION = 0.5667
TIME_ZONES = [-12, -9.5, -7, -5, 0, 1, 2, 5.5, 5.75, 9, 12.75, 14]
EPOCH = dt.datetime(1970, 1, 1)
UNIX_1900 = (dt.datetime(1900, 1, 1) - EPOCH).total_seconds()
UNIX_2100 = (dt.datetime(2100, 1, 1) - EPOCH).total_seconds()


def draw(rng):
    unix = float(rng.integers(int(UNIX_1900) // 60, int(UNIX_2100) // 60)) * 60
    unix += int(rng.integers(0, 60)) + float(rng.choice([0, 0.25, 0.5]))
    return dict(
        unix=unix,
        time_zone=float(rng.choice(TIME_ZONES)),
        latitude=float(rng.uniform(-89.9, 89.9)),
        longitude=float(rng.uniform(-180, 180)),
        elevation=float(rng.uniform(0, 4000)),
        pressure=float(rng.uniform(600, 1050)),
        temperature=float(rng.uniform(-30, 40)),
        delta_t=float(rng.uniform(30, 90)),
    )


def solve(p):
    theta, _, e, _, phi, _ = spa.solar_position(
        np.array([p["unix"]]), p["latitude"], p["longitude"], p["elevation"], p["pressure"],
        p["temperature"], p["delta_t"], REFRACTION, numthreads=1)
    return float(theta[0]), float(e[0]), float(phi[0])


def row(p, zenith, azimuth):
    local = EPOCH + dt.timedelta(seconds=p["unix"] + p["time_zone"] * 3600)
    second = local.second + local.microsecond / 1e6
    assert 1900 <= local.year <= 2101 and 0 <= second < 60
    return [local.year, local.month, local.day, local.hour, local.minute, repr(second), repr(p["time_zone"]),
            repr(p["latitude"]), repr(p["longitude"]), repr(p["elevation"]), repr(p["pressure"]),
            repr(p["temperature"]), repr(p["delta_t"]), repr(zenith), repr(azimuth)]


def main():
    rng = np.random.default_rng(SEED)
    rows = []
    for _ in range(800):
        p = draw(rng)
        zenith, _, azimuth = solve(p)
        rows.append(row(p, zenith, azimuth))
    near = 0
    while near < 200:
        p = draw(rng)
        zenith, elevation, azimuth = solve(p)
        if -2 <= elevation <= 2:
            rows.append(row(p, zenith, azimuth))
            near += 1
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "spa_reference.csv")
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(["year", "month", "day", "hour", "minute", "second", "time_zone", "latitude", "longitude",
                    "elevation", "pressure", "temperature", "delta_t", "zenith", "azimuth"])
        w.writerows(rows)
    print(f"{len(rows)} rows")


if __name__ == "__main__":
    main()
