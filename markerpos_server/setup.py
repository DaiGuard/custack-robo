import os
from setuptools import setup, find_packages

PACKAGE_NAME = "markerpos_server"
MARKERPOS_SERV = "markerpos_server"
HOMOGRAPH_CALC = "homography_calc"
REQUIREMENTS_PATH = os.path.join(os.path.dirname(__file__), "requirements.txt")

def get_requirements():
    with open(REQUIREMENTS_PATH, 'r', encoding='utf-8') as f:
        requirements = [line.strip() for line in f if line.strip() and not line.startswith('#')]
    return requirements

setup(
    name=PACKAGE_NAME,
    version="0.1",
    # packages=find_packages(),
    install_requires=get_requirements(),
    python_requires=">=3.10",
    entry_points={
        "console_scripts": [
            f"{MARKERPOS_SERV}={PACKAGE_NAME}.{MARKERPOS_SERV}:main",
            f"{HOMOGRAPH_CALC}={PACKAGE_NAME}.{HOMOGRAPH_CALC}:main"
        ]
    },
)