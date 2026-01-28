# AWS Deployment Guide for PastryManager

## Prerequisites

1. AWS Account with appropriate permissions
2. AWS CLI installed and configured: `aws --version`
3. Docker installed for building container images
4. .NET 8 SDK

## Deployment Options

### Option 1: AWS App Runner (Easiest - Recommended)

AWS App Runner is the simplest way to deploy containerized applications to AWS.

#### 1. Create RDS PostgreSQL Database
```bash
# Create security group for RDS
aws ec2 create-security-group \
  --group-name pastrymanager-db-sg \
  --description "Security group for PastryManager PostgreSQL database"

# Allow PostgreSQL traffic from App Runner (will update after creating service)
aws ec2 authorize-security-group-ingress \
  --group-name pastrymanager-db-sg \
  --protocol tcp \
  --port 5432 \
  --cidr 0.0.0.0/0

# Create RDS PostgreSQL instance
aws rds create-db-instance \
  --db-instance-identifier pastrymanager-db \
  --db-instance-class db.t3.micro \
  --engine postgres \
  --engine-version 14.9 \
  --master-username pastrydbadmin \
  --master-user-password 'YourSecurePassword123!' \
  --allocated-storage 20 \
  --vpc-security-group-ids sg-xxxxxxxx \
  --publicly-accessible \
  --backup-retention-period 7 \
  --preferred-backup-window "03:00-04:00" \
  --preferred-maintenance-window "mon:04:00-mon:05:00"

# Wait for database to be available (5-10 minutes)
aws rds wait db-instance-available --db-instance-identifier pastrymanager-db

# Get database endpoint
aws rds describe-db-instances \
  --db-instance-identifier pastrymanager-db \
  --query 'DBInstances[0].Endpoint.Address' \
  --output text
```

#### 2. Build and Push Docker Image to ECR
```bash
# Create ECR repository
aws ecr create-repository --repository-name pastrymanager-api

# Login to ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin <aws_account_id>.dkr.ecr.us-east-1.amazonaws.com

# Build Docker image
docker build -t pastrymanager-api:latest -f Dockerfile .

# Tag image for ECR
docker tag pastrymanager-api:latest <aws_account_id>.dkr.ecr.us-east-1.amazonaws.com/pastrymanager-api:latest

# Push to ECR
docker push <aws_account_id>.dkr.ecr.us-east-1.amazonaws.com/pastrymanager-api:latest
```

#### 3. Create App Runner Service
```bash
# Create service with environment variables
aws apprunner create-service \
  --service-name pastrymanager-api \
  --source-configuration '{
    "ImageRepository": {
      "ImageIdentifier": "<aws_account_id>.dkr.ecr.us-east-1.amazonaws.com/pastrymanager-api:latest",
      "ImageRepositoryType": "ECR",
      "ImageConfiguration": {
        "Port": "8080",
        "RuntimeEnvironmentVariables": {
          "ASPNETCORE_ENVIRONMENT": "Production",
          "ConnectionStrings__DefaultConnection": "Host=<rds-endpoint>;Database=pastrydb;Username=pastrydbadmin;Password=YourSecurePassword123!;SSL Mode=Require"
        }
      }
    },
    "AutoDeploymentsEnabled": true
  }' \
  --instance-configuration '{
    "Cpu": "1024",
    "Memory": "2048"
  }' \
  --health-check-configuration '{
    "Protocol": "HTTP",
    "Path": "/health/live",
    "Interval": 10,
    "Timeout": 5,
    "HealthyThreshold": 1,
    "UnhealthyThreshold": 5
  }'

# Get service URL
aws apprunner describe-service \
  --service-arn <service-arn> \
  --query 'Service.ServiceUrl' \
  --output text
```

### Option 2: AWS ECS Fargate (More Control)

#### 1. Create ECS Cluster
```bash
aws ecs create-cluster --cluster-name pastrymanager-cluster
```

#### 2. Create Task Definition
Create `task-definition.json`:
```json
{
  "family": "pastrymanager-api",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "512",
  "memory": "1024",
  "containerDefinitions": [{
    "name": "pastrymanager-api",
    "image": "<aws_account_id>.dkr.ecr.us-east-1.amazonaws.com/pastrymanager-api:latest",
    "portMappings": [{
      "containerPort": 8080,
      "protocol": "tcp"
    }],
    "environment": [
      {
        "name": "ASPNETCORE_ENVIRONMENT",
        "value": "Production"
      },
      {
        "name": "ConnectionStrings__DefaultConnection",
        "value": "Host=<rds-endpoint>;Database=pastrydb;Username=pastrydbadmin;Password=YourSecurePassword123!;SSL Mode=Require"
      }
    ],
    "healthCheck": {
      "command": ["CMD-SHELL", "curl -f http://localhost:8080/health/live || exit 1"],
      "interval": 30,
      "timeout": 5,
      "retries": 3
    },
    "logConfiguration": {
      "logDriver": "awslogs",
      "options": {
        "awslogs-group": "/ecs/pastrymanager-api",
        "awslogs-region": "us-east-1",
        "awslogs-stream-prefix": "ecs"
      }
    }
  }]
}
```

Register task definition:
```bash
aws ecs register-task-definition --cli-input-json file://task-definition.json
```

#### 3. Create Application Load Balancer
```bash
# Create security group for ALB
aws ec2 create-security-group \
  --group-name pastrymanager-alb-sg \
  --description "Security group for PastryManager ALB"

# Allow HTTP/HTTPS traffic
aws ec2 authorize-security-group-ingress \
  --group-name pastrymanager-alb-sg \
  --protocol tcp --port 80 --cidr 0.0.0.0/0

aws ec2 authorize-security-group-ingress \
  --group-name pastrymanager-alb-sg \
  --protocol tcp --port 443 --cidr 0.0.0.0/0

# Create ALB
aws elbv2 create-load-balancer \
  --name pastrymanager-alb \
  --subnets subnet-xxxxxx subnet-yyyyyy \
  --security-groups sg-xxxxxxxx

# Create target group
aws elbv2 create-target-group \
  --name pastrymanager-tg \
  --protocol HTTP \
  --port 8080 \
  --vpc-id vpc-xxxxxx \
  --target-type ip \
  --health-check-path /health/ready \
  --health-check-interval-seconds 30 \
  --healthy-threshold-count 2 \
  --unhealthy-threshold-count 3

# Create listener
aws elbv2 create-listener \
  --load-balancer-arn <alb-arn> \
  --protocol HTTP \
  --port 80 \
  --default-actions Type=forward,TargetGroupArn=<target-group-arn>
```

#### 4. Create ECS Service
```bash
aws ecs create-service \
  --cluster pastrymanager-cluster \
  --service-name pastrymanager-api \
  --task-definition pastrymanager-api \
  --desired-count 2 \
  --launch-type FARGATE \
  --network-configuration "awsvpcConfiguration={subnets=[subnet-xxxxxx,subnet-yyyyyy],securityGroups=[sg-xxxxxxxx],assignPublicIp=ENABLED}" \
  --load-balancers "targetGroupArn=<target-group-arn>,containerName=pastrymanager-api,containerPort=8080"
```

### Option 3: AWS Elastic Beanstalk (Traditional)

#### 1. Install EB CLI
```bash
pip install awsebcli
```

#### 2. Initialize Elastic Beanstalk
```bash
cd PastryManager
eb init -p docker pastrymanager-api --region us-east-1
```

#### 3. Create Dockerrun.aws.json
```json
{
  "AWSEBDockerrunVersion": "1",
  "Image": {
    "Name": "<aws_account_id>.dkr.ecr.us-east-1.amazonaws.com/pastrymanager-api:latest"
  },
  "Ports": [
    {
      "ContainerPort": 8080,
      "HostPort": 8080
    }
  ]
}
```

#### 4. Deploy
```bash
eb create pastrymanager-env --database.engine postgres --database.username pastrydbadmin
eb setenv ConnectionStrings__DefaultConnection="Host=<rds-endpoint>;Database=pastrydb;Username=pastrydbadmin;Password=YourSecurePassword123!;SSL Mode=Require"
eb deploy
```

## Configuration

### Connection String Format

**Local (Aspire):**
```json
{
  "ConnectionStrings": {
    "pastrydb": "Host=localhost;Port=5432;Database=pastrydb;Username=postgres;Password=postgres"
  }
}
```

**AWS RDS PostgreSQL:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<rds-endpoint>.rds.amazonaws.com;Database=pastrydb;Username=pastrydbadmin;Password=YourSecurePassword123!;SSL Mode=Require"
  }
}
```

### Environment Variables

Set via AWS Console, CLI, or infrastructure as code:
- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection=<postgresql-connection-string>`

## Security Best Practices

### 1. Use AWS Secrets Manager
```bash
# Store database password
aws secretsmanager create-secret \
  --name pastrymanager/db/password \
  --secret-string 'YourSecurePassword123!'

# Get secret ARN
aws secretsmanager describe-secret \
  --secret-id pastrymanager/db/password \
  --query ARN --output text
```

Update task definition to use secrets:
```json
"secrets": [
  {
    "name": "DB_PASSWORD",
    "valueFrom": "arn:aws:secretsmanager:region:account:secret:pastrymanager/db/password"
  }
]
```

### 2. Configure RDS Security
```bash
# Update security group to allow only from App Runner/ECS
aws ec2 authorize-security-group-ingress \
  --group-id sg-xxxxxxxx \
  --protocol tcp \
  --port 5432 \
  --source-group sg-yyyyyyyy
```

### 3. Enable RDS Encryption
```bash
aws rds modify-db-instance \
  --db-instance-identifier pastrymanager-db \
  --storage-encrypted \
  --apply-immediately
```

## Monitoring with CloudWatch

### Enable CloudWatch Logs
Already configured in ECS task definition. View logs:
```bash
aws logs tail /ecs/pastrymanager-api --follow
```

### Create CloudWatch Alarms
```bash
# High CPU alarm
aws cloudwatch put-metric-alarm \
  --alarm-name pastrymanager-high-cpu \
  --alarm-description "Alert when CPU exceeds 80%" \
  --metric-name CPUUtilization \
  --namespace AWS/ECS \
  --statistic Average \
  --period 300 \
  --threshold 80 \
  --comparison-operator GreaterThanThreshold \
  --evaluation-periods 2

# Database connections alarm
aws cloudwatch put-metric-alarm \
  --alarm-name pastrymanager-db-connections \
  --alarm-description "Alert when DB connections exceed 80" \
  --metric-name DatabaseConnections \
  --namespace AWS/RDS \
  --statistic Average \
  --period 300 \
  --threshold 80 \
  --comparison-operator GreaterThanThreshold
```

## Auto-Scaling Configuration

### App Runner Auto-Scaling
```bash
aws apprunner update-service \
  --service-arn <service-arn> \
  --auto-scaling-configuration-arn arn:aws:apprunner:region:account:autoscalingconfiguration/DefaultConfiguration
```

### ECS Auto-Scaling
```bash
# Register scalable target
aws application-autoscaling register-scalable-target \
  --service-namespace ecs \
  --resource-id service/pastrymanager-cluster/pastrymanager-api \
  --scalable-dimension ecs:service:DesiredCount \
  --min-capacity 2 \
  --max-capacity 10

# Create scaling policy
aws application-autoscaling put-scaling-policy \
  --service-namespace ecs \
  --resource-id service/pastrymanager-cluster/pastrymanager-api \
  --scalable-dimension ecs:service:DesiredCount \
  --policy-name cpu-scaling \
  --policy-type TargetTrackingScaling \
  --target-tracking-scaling-policy-configuration file://scaling-policy.json
```

## CI/CD with GitHub Actions

Create `.github/workflows/deploy-aws.yml`:
```yaml
name: Deploy to AWS

on:
  push:
    branches: [ main ]

env:
  AWS_REGION: us-east-1
  ECR_REPOSITORY: pastrymanager-api
  ECS_SERVICE: pastrymanager-api
  ECS_CLUSTER: pastrymanager-cluster

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Configure AWS credentials
      uses: aws-actions/configure-aws-credentials@v2
      with:
        aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
        aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
        aws-region: ${{ env.AWS_REGION }}
    
    - name: Login to Amazon ECR
      id: login-ecr
      uses: aws-actions/amazon-ecr-login@v1
    
    - name: Build and push image
      env:
        ECR_REGISTRY: ${{ steps.login-ecr.outputs.registry }}
        IMAGE_TAG: ${{ github.sha }}
      run: |
        docker build -t $ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG -f Dockerfile .
        docker push $ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG
        docker tag $ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG $ECR_REGISTRY/$ECR_REPOSITORY:latest
        docker push $ECR_REGISTRY/$ECR_REPOSITORY:latest
    
    - name: Update ECS service
      run: |
        aws ecs update-service \
          --cluster ${{ env.ECS_CLUSTER }} \
          --service ${{ env.ECS_SERVICE }} \
          --force-new-deployment
```

## Health Check Endpoints

The API includes these health check endpoints for AWS:
- `/health` - Overall health
- `/health/ready` - Readiness probe (includes database check) - Use for target groups
- `/health/live` - Liveness probe - Use for container health checks

## Cost Estimation

### Monthly Costs (us-east-1):

**Option 1: App Runner**
- App Runner (1 vCPU, 2GB RAM): ~$25/month
- RDS db.t3.micro: ~$15/month
- Data transfer: ~$5/month
- **Total: ~$45/month**

**Option 2: ECS Fargate**
- Fargate (2 tasks, 0.5 vCPU, 1GB each): ~$30/month
- Application Load Balancer: ~$16/month
- RDS db.t3.micro: ~$15/month
- CloudWatch Logs: ~$5/month
- **Total: ~$66/month**

**Option 3: Elastic Beanstalk**
- EC2 t3.micro: ~$7.5/month
- Load Balancer: ~$16/month
- RDS db.t3.micro: ~$15/month
- **Total: ~$38.5/month**

## Troubleshooting

### View Logs
```bash
# App Runner
aws logs tail /aws/apprunner/pastrymanager-api --follow

# ECS
aws logs tail /ecs/pastrymanager-api --follow

# RDS Logs
aws rds describe-db-log-files --db-instance-identifier pastrymanager-db
```

### Test Database Connection
```bash
psql "host=<rds-endpoint>.rds.amazonaws.com port=5432 dbname=pastrydb user=pastrydbadmin password=YourSecurePassword123! sslmode=require"
```

### Check Service Health
```bash
curl https://<app-url>/health
curl https://<app-url>/health/ready
curl https://<app-url>/health/live
```

## Database Migrations

Migrations run automatically on application startup in development. For production:

1. **Manual approach**: Run migrations from your local machine:
```bash
dotnet ef database update --project PastryManager.Infrastructure --startup-project PastryManager
```

2. **Automated approach**: Include in deployment pipeline before service update.

## Backup and Disaster Recovery

### RDS Automated Backups
```bash
aws rds modify-db-instance \
  --db-instance-identifier pastrymanager-db \
  --backup-retention-period 7 \
  --preferred-backup-window "03:00-04:00"
```

### Manual Snapshot
```bash
aws rds create-db-snapshot \
  --db-snapshot-identifier pastrymanager-manual-snapshot-$(date +%Y%m%d) \
  --db-instance-identifier pastrymanager-db
```

## Cleanup

To avoid ongoing charges, delete resources:
```bash
# Delete App Runner service
aws apprunner delete-service --service-arn <service-arn>

# Delete RDS instance
aws rds delete-db-instance \
  --db-instance-identifier pastrymanager-db \
  --skip-final-snapshot

# Delete ECR repository
aws ecr delete-repository --repository-name pastrymanager-api --force
```
