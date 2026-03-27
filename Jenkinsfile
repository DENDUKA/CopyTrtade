pipeline {
    agent any

    parameters {
        booleanParam(name: 'SKIP_TESTS', defaultValue: false, description: 'Пропустить тесты')
        booleanParam(name: 'FORCE_DEPLOY', defaultValue: true, description: 'Перезапустить контейнер приложения после сборки')
        string(name: 'IMAGE_TAG_OVERRIDE', defaultValue: '', description: 'Переопределить тег образа')
    }

    environment {
        DOCKER_IMAGE_NAME = 'copytrading-app'
        COMPOSE_DOCKER_CLI_BUILD = '1'
        DOCKER_BUILDKIT = '1'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    }

    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 30, unit: 'MINUTES')
        disableConcurrentBuilds()
        timestamps()
    }

    triggers {
        pollSCM('H/2 * * * *')
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
                script {
                    def shortCommit = sh(script: 'git rev-parse --short HEAD', returnStdout: true).trim()
                    def branch = (env.BRANCH_NAME ?: 'local').replaceAll('/', '-')
                    env.GIT_SHORT_COMMIT = shortCommit
                    env.BUILD_BRANCH = branch
                    env.IMAGE_TAG = params.IMAGE_TAG_OVERRIDE?.trim()
                        ? params.IMAGE_TAG_OVERRIDE.trim()
                        : "${branch}-${shortCommit}-${env.BUILD_NUMBER}"
                    echo "Branch=${env.BUILD_BRANCH} Commit=${env.GIT_SHORT_COMMIT} Tag=${env.IMAGE_TAG}"
                }
            }
        }

        stage('Test') {
            when { not { expression { params.SKIP_TESTS } } }
            steps {
                sh '''
                    docker run --rm \
                      -v "$PWD:/src" \
                      -w /src \
                      mcr.microsoft.com/dotnet/sdk:8.0 \
                      dotnet test CopyTriding.Test/CopyTrading.Test.csproj \
                      -c Release \
                      --verbosity normal
                '''
            }
        }

        stage('Docker Build') {
            steps {
                sh '''
                    docker build \
                      --build-arg BUILD_CONFIGURATION=Release \
                      --label git.commit=${GIT_SHORT_COMMIT} \
                      --label git.branch=${BUILD_BRANCH} \
                      --label build.number=${BUILD_NUMBER} \
                      -t ${DOCKER_IMAGE_NAME}:${IMAGE_TAG} \
                      -t ${DOCKER_IMAGE_NAME}:latest \
                      -f Dockerfile .
                '''
            }
        }

        stage('Deploy') {
            when { expression { params.FORCE_DEPLOY } }
            steps {
                sh '''
                    IMAGE_TAG=${IMAGE_TAG} DOCKER_IMAGE_NAME=${DOCKER_IMAGE_NAME} \
                    docker-compose -f docker-compose.app.yml up -d --no-build
                '''
                sh 'docker-compose -f docker-compose.app.yml ps'
            }
        }
    }

    post {
        success {
            echo "SUCCESS — ${env.DOCKER_IMAGE_NAME}:${env.IMAGE_TAG}"
        }
        failure {
            echo "FAILED — branch: ${env.BUILD_BRANCH}, build: ${env.BUILD_NUMBER}"
        }
        always {
            sh 'docker image prune -f || true'
        }
    }
}

